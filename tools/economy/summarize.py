"""Build an evidence-backed playtest summary from collector JSONL packets (stdlib only)."""
import argparse
import collections
import datetime as dt
import json
from pathlib import Path


def utc(value):
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(dt.timezone.utc)


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def evidence(event):
    return {"event_id": event["event_id"], "file": event["_file"], "line": event["_line"]}


RUN_RESOURCES = ("coins", "crystals")


def empty_flow_totals():
    return {f"{resource}_income": 0 for resource in RUN_RESOURCES} | {
        f"{resource}_expense": 0 for resource in RUN_RESOURCES
    }


def empty_resource_totals():
    return {resource: 0 for resource in RUN_RESOURCES}


def update_flow_totals(target, resource, income=0, expense=0):
    target[f"{resource}_income"] += income
    target[f"{resource}_expense"] += expense


def ensure_run(runs, run_id, level=None):
    run = runs.setdefault(run_id, {"run_id": run_id, "level": level, "active_seconds": 0,
                                   "outcome": "unfinished", "result": "unfinished",
                                   "termination_reason": "unfinished", "remaining_lives": None,
                                   "has_start": False, "confirmed": False, "evidence": [],
                                   "loot": empty_flow_totals(), "loot_sources": {},
                                   "expenses": empty_resource_totals(), "expense_sources": {},
                                   "net": {"xp": 0, "points": 0, "coins": 0, "crystals": 0}})
    if level and not run.get("level"):
        run["level"] = level
    return run


def add_run_flows(run, flows):
    for flow in flows or []:
        resource = flow.get("resource")
        if resource not in RUN_RESOURCES:
            continue
        income = max(0, flow.get("income", 0))
        expense = max(0, flow.get("expense", 0))
        update_flow_totals(run["loot"], resource, income, expense)
        source = flow.get("source") or "unknown"
        bucket = run["loot_sources"].setdefault(source, empty_flow_totals())
        update_flow_totals(bucket, resource, income, expense)


def add_run_expense(run, source, resource, amount):
    if amount <= 0 or resource not in RUN_RESOURCES:
        return
    run["expenses"][resource] += amount
    bucket = run["expense_sources"].setdefault(source or "unknown", empty_resource_totals())
    bucket[resource] += amount


def classify_run_result(reason):
    if reason in ("win", "tutorial_completed"):
        return "win"
    if reason == "loss":
        return "loss"
    if reason == "unfinished":
        return "unfinished"
    return "abandoned"


def read_events(root):
    events, issues, uploads = {}, [], []
    duplicates = 0
    for file in sorted(root.rglob("*.jsonl")):
        metadata = file.with_suffix(".metadata.json")
        if metadata.exists():
            try:
                uploads.append(json.loads(metadata.read_text(encoding="utf-8-sig"))["receivedAtUtc"])
            except (ValueError, KeyError) as error:
                issues.append({"kind": "invalid_metadata", "file": str(metadata), "error": str(error)})
        for number, line in enumerate(file.read_text(encoding="utf-8-sig").splitlines(), 1):
            if not line.strip():
                continue
            try:
                item = json.loads(line)
                for name in ("event_id", "profile_id", "session_id", "type", "utc", "sequence"):
                    if name not in item:
                        raise ValueError("missing " + name)
                if item.get("schema_version") != 1:
                    raise ValueError("unsupported schema_version")
                if not isinstance(item["sequence"], int):
                    raise ValueError("sequence must be an integer")
                utc(item["utc"])
                key = item["event_id"]
                raw = canonical(item)
                if key in events:
                    duplicates += 1
                    if events[key]["_raw"] != raw:
                        issues.append({"kind": "event_conflict", "event_id": key, "file": str(file), "line": number})
                    continue
                item.update(_file=str(file.resolve()), _line=number, _raw=raw)
                events[key] = item
            except (ValueError, TypeError, AttributeError, KeyError) as error:
                issues.append({"kind": "invalid_event", "file": str(file), "line": number, "error": str(error)})
    return list(events.values()), issues, duplicates, max(uploads, key=utc, default=None)


def summarize_group(events):
    # Within a session the sequence remains authoritative if the device clock changes.
    sessions = collections.defaultdict(list)
    for event in events:
        sessions[event["session_id"]].append(event)
    ordered, issues = [], []
    for _, items in sorted(sessions.items(), key=lambda pair: min(utc(e["utc"]) for e in pair[1])):
        items.sort(key=lambda e: e["sequence"])
        for event in items:
            if event.get("_clock_reversed"):
                issues.append({"kind": "clock_reversed", **evidence(event)})
            if event.get("_sequence_gap"):
                issues.append({"kind": "sequence_gap", "missing": event["_sequence_gap"], **evidence(event)})
        ordered.extend(items)

    sources = collections.defaultdict(lambda: {"xp": 0, "coins_income": 0, "coins_expense": 0,
                                                "crystals_income": 0, "crystals_expense": 0, "points_net": 0})
    operations, op_ids, runs, milestones = [], {}, {}, []
    funnel = collections.Counter()
    first, last = None, None
    features = {}
    last_checkpoint = None
    started_count = 0
    total_active = 0.0
    for event in ordered:
        run_id = event.get("run_id")
        if event.get("lost_packets", 0):
            issues.append({"kind": "queue_loss", "lost_packets": event["lost_packets"], **evidence(event)})
        if event["type"] == "data_gap":
            issues.append({"kind": event.get("source"), **evidence(event)})
        if event["type"] == "session_started":
            features = {key: event.get(key) for key in ("ads_test_mode", "purchases_enabled", "interstitial_enabled")}
            snapshot = event.get("after")
            if snapshot:
                first = first or snapshot
                last = snapshot
                last_checkpoint = snapshot
        if run_id:
            run = ensure_run(runs, run_id, event.get("level"))
            seconds = max(0, event.get("active_seconds", 0))
            total_active += max(0, seconds - run["active_seconds"])
            run["active_seconds"] = max(run["active_seconds"], seconds)
            if event["type"] == "run_started":
                run["has_start"] = True
                started_count += 1
                run["before"] = event.get("before")
                run["previous_best_stars"] = event.get("previous_best_stars", 0)
                run["attempt_kind"] = event.get("source")
                run["evidence"].append(evidence(event))
            elif event["type"] == "run_finished":
                reason = event.get("source") or "unfinished"
                run.update(outcome=reason, result=classify_run_result(reason), termination_reason=reason,
                           stars=event.get("stars"), after=event.get("after"),
                           confirmed=event.get("confirmed", False))
                if event.get("remaining_lives", -1) >= 0:
                    run["remaining_lives"] = event.get("remaining_lives")
                add_run_flows(run, event.get("flows"))
                run["evidence"].append(evidence(event))
        if event["type"] in ("monetization", "return_activity", "first_session"):
            # Placement is before the hashed correlation ID; no raw purchase receipts are present.
            placement = (event.get("detail") or "").split(":", 1)[0] if event["type"] == "monetization" else ""
            funnel[(event["type"], event.get("source", ""), placement)] += 1
        if event["type"] != "economy_transaction" or not event.get("confirmed"):
            continue
        operation_id = event.get("operation_id")
        comparable = {k: event.get(k) for k in ("before", "after", "source", "xp_delta", "coins_delta", "crystals_delta", "points_delta", "flows")}
        if operation_id in op_ids:
            if op_ids[operation_id] != comparable:
                issues.append({"kind": "operation_conflict", "operation_id": operation_id, **evidence(event)})
            continue
        op_ids[operation_id] = comparable
        before, after = event["before"], event["after"]
        if last_checkpoint and canonical(before) != canonical(last_checkpoint):
            issues.append({"kind": "checkpoint_gap", **evidence(event)})
        first, last, last_checkpoint = first or before, after, after
        source = event.get("source") or "unknown"
        bucket = sources[source]
        xp = event.get("xp_delta", 0)
        bucket["xp"] += xp
        bucket["points_net"] += event.get("points_delta", 0)
        if run_id:
            run = ensure_run(runs, run_id, event.get("level"))
            add_run_flows(run, event.get("flows"))
            run["net"]["xp"] += xp
            run["net"]["points"] += event.get("points_delta", 0)
            run["net"]["coins"] += event.get("coins_delta", 0)
            run["net"]["crystals"] += event.get("crystals_delta", 0)
        for resource in ("coins", "crystals"):
            delta = event.get(resource + "_delta", 0)
            flows = [f for f in event.get("flows") or [] if f["resource"] == resource]
            flow_net = 0
            for flow in flows:
                sources[flow["source"]][resource + "_income"] += flow["income"]
                sources[flow["source"]][resource + "_expense"] += flow["expense"]
                flow_net += flow["income"] - flow["expense"]
            remaining = delta - flow_net
            bucket[resource + ("_income" if remaining >= 0 else "_expense")] += abs(remaining)
            if run_id and remaining < 0:
                add_run_expense(run, source, resource, abs(remaining))
        operations.append({"utc": event["utc"], "source": source, "detail": event.get("detail"),
                           "operation_id": operation_id, "run_id": run_id, "xp": xp,
                           "coins_net": event.get("coins_delta", 0), "crystals_net": event.get("crystals_delta", 0),
                           "player_level": after["player_level"], "xp_remainder": after["xp"], **evidence(event)})
        changes = {key: after.get(key) for key in ("player_level", "skins", "abilities", "upgrades") if before.get(key) != after.get(key)}
        if changes:
            milestones.append({"utc": event["utc"], "active_minutes_since_period_start": round(total_active / 60, 3),
                               "attempts_since_period_start": started_count, "changes": changes, **evidence(event)})

    levels = collections.defaultdict(lambda: {"attempts": 0, "active_seconds": 0, "outcomes": collections.Counter()})
    for run in runs.values():
        level = levels[run["level"] or "unknown"]
        level["attempts"] += 1
        level["active_seconds"] += run["active_seconds"]
        level["outcomes"][run["outcome"]] += 1
        if not run["has_start"] or run["outcome"] in ("unfinished", "interrupted", "profile_changed"):
            issues.append({"kind": "incomplete_run", "run_id": run["run_id"]})
    for item in levels.values():
        item["active_seconds"] = round(item["active_seconds"], 3)
        item["outcomes"] = dict(sorted(item["outcomes"].items()))
    active = sum(r["active_seconds"] for r in runs.values())
    xp = sum(s["xp"] for s in sources.values())
    dates = sorted({utc(e["utc"]).date().isoformat() for e in events})
    visit_times = sorted([min(utc(e["utc"]) for e in items).isoformat() for items in sessions.values()])
    return {"profile_id": events[0]["profile_id"], "save_generation": events[0].get("save_generation"),
            "balance_version": events[0].get("balance_version"), "cohort": events[0].get("cohort"),
            "build_versions": sorted({e.get("build_version", "") for e in events}),
            "first_event_utc": min(e["utc"] for e in events), "last_event_utc": max(e["utc"] for e in events),
            "events": len(events), "sessions": len(sessions), "dates_utc": dates,
            "feature_configuration": features,
            "session_starts_utc": visit_times,
            "session_intervals_hours": [round((utc(b) - utc(a)).total_seconds() / 3600, 3) for a, b in zip(visit_times, visit_times[1:])],
            "active_minutes": round(active / 60, 3), "xp": xp,
            "xp_per_active_minute": round(xp * 60 / active, 3) if active else None,
            "tutorial_xp": sources.get("TutorialCompleted", {}).get("xp", 0),
            "sources": dict(sorted(sources.items())), "levels": dict(sorted(levels.items())),
            "initial_state": first, "final_state": last, "milestones": milestones, "operations": operations,
            "runs": list(runs.values()), "funnel": [{"type": key[0], "phase": key[1], "placement": key[2], "count": count}
                                                       for key, count in sorted(funnel.items())],
            "quality": {"complete_observed_period": not issues, "issues": issues}}


def build_summary(root, profile=None, since=None, until=None, balance=None, cohort="playtest"):
    events, issues, duplicates, latest_upload = read_events(root)
    # Check continuity before filtering: a profile switch is not a lost packet.
    sessions = collections.defaultdict(list)
    for event in events:
        sessions[event["session_id"]].append(event)
    for items in sessions.values():
        items.sort(key=lambda event: event["sequence"])
        for previous, current in zip(items, items[1:]):
            current["_sequence_gap"] = max(0, current["sequence"] - previous["sequence"] - 1)
            current["_clock_reversed"] = utc(current["utc"]) < utc(previous["utc"])
    inventory = sorted({(e["profile_id"], e.get("balance_version", ""), e.get("cohort", "")) for e in events})
    selected = [e for e in events if (not profile or e["profile_id"] == profile)
                and (not balance or e.get("balance_version") == balance)
                and (cohort == "all" or e.get("cohort") == cohort)
                and (not since or utc(e["utc"]) >= utc(since)) and (not until or utc(e["utc"]) < utc(until))]
    groups = collections.defaultdict(list)
    for event in selected:
        groups[(event["profile_id"], event.get("save_generation", ""), event.get("balance_version", ""), event.get("cohort", ""))].append(event)
    summaries = [summarize_group(items) for _, items in sorted(groups.items())]
    if issues:
        for group in summaries:
            group["quality"]["complete_observed_period"] = False
    return {"schema_version": 1, "archive": str(root.resolve()), "latest_upload_utc": latest_upload,
            "selection": {"profile": profile, "from_inclusive": since, "to_exclusive": until, "balance": balance, "cohort": cohort},
            "available": [{"profile": p, "balance": b, "cohort": c} for p, b, c in inventory],
            "duplicate_events_removed": duplicates, "input_issues": issues,
            "groups": summaries,
            "notes": ["Active time of interrupted and app-closed runs is a lower bound; progress is sampled every 15 seconds.",
                      "Selected time boundaries may cut sessions/runs; milestones are relative to the selected period.",
                      "Confirmed transactions alone contribute rewards; snapshots and funnel events are controls.",
                      "Input issues invalidate completeness. Zero events do not prove zero activity.",
                      "One profile describes one trajectory, not population retention or platform revenue."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--output", type=Path, default=Path("summary.json"))
    parser.add_argument("--profile")
    parser.add_argument("--from", dest="since")
    parser.add_argument("--to", dest="until")
    parser.add_argument("--balance")
    parser.add_argument("--cohort", choices=("playtest", "dev", "editor", "all"), default="playtest")
    args = parser.parse_args()
    if not args.archive.is_dir():
        parser.error("archive directory does not exist")
    result = build_summary(args.archive, args.profile, args.since, args.until, args.balance, args.cohort)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output.resolve()), "groups": len(result["groups"]),
                      "input_issues": len(result["input_issues"])}))


if __name__ == "__main__":
    main()
