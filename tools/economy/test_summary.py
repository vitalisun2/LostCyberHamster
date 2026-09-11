import copy
import json
import tempfile
import unittest
from pathlib import Path
from summarize import build_summary


def snapshot(coins=0, crystals=0, xp=0, level=1):
    return dict(coins=coins, crystals=crystals, xp=xp, player_level=level, development_points=level - 1,
                skins=[0], abilities=[], upgrades=[], quests=[], receipts=[], campaign=[], return_rewards=[])


def event(n, kind, **values):
    return dict(schema_version=1, event_id=f"s:{n}", sequence=n, session_id="s", profile_id="p",
                save_generation="p", balance_version="b1", cohort="playtest", build_version="a1",
                utc=f"2026-09-10T12:00:{n:02d}+00:00", type=kind, **values)


class SummaryChecks(unittest.TestCase):
    def summarize(self, items):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            (path / "packet.jsonl").write_text("\n".join(json.dumps(x) for x in items) + "\n", encoding="utf-8")
            first = build_summary(path)
            self.assertEqual(first, build_summary(path))
            return first

    def test_rollover_and_duplicate_transport(self):
        before, after = snapshot(xp=230), snapshot(xp=21, level=2)
        tx = event(2, "economy_transaction", operation_id="op1", confirmed=True, source="LevelCompleted",
                   before=before, after=after, xp_delta=31, coins_delta=0, crystals_delta=0, points_delta=1)
        result = self.summarize([event(1, "session_started", after=before), tx, tx])
        self.assertEqual(result["groups"][0]["xp"], 31)
        self.assertEqual(result["duplicate_events_removed"], 1)
        self.assertTrue(result["groups"][0]["quality"]["complete_observed_period"])

    def test_income_expense_and_run_time(self):
        a, b, c = snapshot(), snapshot(coins=100), snapshot(coins=20)
        items = [event(1, "session_started", after=a), event(2, "run_started", run_id="r", level="L", before=a),
                 event(3, "run_progress", run_id="r", active_seconds=30),
                 event(4, "economy_transaction", run_id="r", operation_id="ambient", confirmed=True,
                       source="gameplay_checkpoint", before=a, after=b, coins_delta=100,
                       flows=[dict(resource="coins", source="pickup", income=100, expense=0)]),
                 event(5, "economy_transaction", run_id="r", operation_id="purchase", confirmed=True,
                       source="RunRefillPurchased", before=b, after=c, coins_delta=-80),
                 event(6, "run_finished", run_id="r", level="L", active_seconds=60, source="win", after=c)]
        group = self.summarize(items)["groups"][0]
        self.assertEqual(group["active_minutes"], 1)
        self.assertEqual(group["sources"]["pickup"]["coins_income"], 100)
        self.assertEqual(group["sources"]["RunRefillPurchased"]["coins_expense"], 80)
        self.assertEqual(group["final_state"]["coins"], 20)

    def test_conflict_gap_incomplete(self):
        one = event(1, "session_started", after=snapshot())
        conflict = copy.deepcopy(one)
        conflict["after"]["coins"] = 200
        result = self.summarize([one, conflict, event(4, "run_started", run_id="r", level="L")])
        self.assertEqual(result["input_issues"][0]["kind"], "event_conflict")
        kinds = {x["kind"] for x in result["groups"][0]["quality"]["issues"]}
        self.assertTrue({"incomplete_run", "sequence_gap"} <= kinds)

    def test_operation_duplicate_and_conflict(self):
        a, b = snapshot(), snapshot(xp=31)
        tx = event(2, "economy_transaction", operation_id="one", confirmed=True, source="win", before=a, after=b, xp_delta=31)
        retry = dict(tx, event_id="s:3", sequence=3)
        conflict = dict(tx, event_id="s:4", sequence=4, xp_delta=32)
        result = self.summarize([event(1, "session_started", after=a), tx, retry, conflict])["groups"][0]
        self.assertEqual(result["xp"], 31)
        self.assertIn("operation_conflict", {x["kind"] for x in result["quality"]["issues"]})

    def test_dev_profile_excluded(self):
        dev = event(2, "session_started", after=snapshot(coins=10000))
        dev.update(cohort="dev", profile_id="dev")
        result = self.summarize([event(1, "session_started", after=snapshot()), dev])
        self.assertEqual(len(result["groups"]), 1)
        self.assertEqual(len(result["available"]), 2)

    def test_sequential_runs_keep_reason_loot_and_expense_per_attempt(self):
        start = snapshot(coins=0, crystals=3)
        after_loss_loot = snapshot(coins=25, crystals=3)
        after_win_loot = snapshot(coins=65, crystals=3)
        after_revive_purchase = snapshot(coins=65, crystals=2)
        after_restart_loot = snapshot(coins=76, crystals=2)
        group = self.summarize([
            event(1, "session_started", after=start),
            event(2, "run_started", run_id="r1", level="L1", before=start),
            event(3, "economy_transaction", run_id="r1", operation_id="ambient-1", confirmed=True,
                  source="gameplay_checkpoint", before=start, after=after_loss_loot, coins_delta=25,
                  crystals_delta=0, points_delta=0,
                  flows=[dict(resource="coins", source="pickup", income=25, expense=0)]),
            event(4, "run_finished", run_id="r1", level="L1", active_seconds=30, source="loss",
                  remaining_lives=0, after=after_loss_loot),
            event(5, "run_started", run_id="r2", level="L1", before=after_loss_loot),
            event(6, "economy_transaction", run_id="r2", operation_id="ambient-2", confirmed=True,
                  source="gameplay_checkpoint", before=after_loss_loot, after=after_win_loot, coins_delta=40,
                  crystals_delta=0, points_delta=0,
                  flows=[dict(resource="coins", source="pickup", income=30, expense=0),
                         dict(resource="coins", source="obstacle_bonus", income=10, expense=0)]),
            event(7, "economy_transaction", run_id="r2", operation_id="revive-1", confirmed=True,
                  source="RunRevivePurchased", before=after_win_loot, after=after_revive_purchase, coins_delta=0,
                  crystals_delta=-1, points_delta=0),
            event(8, "run_finished", run_id="r2", level="L1", active_seconds=55, source="win", stars=2,
                  remaining_lives=2, confirmed=True, after=after_revive_purchase),
            event(9, "run_started", run_id="r3", level="L1", before=after_revive_purchase),
            event(10, "run_finished", run_id="r3", level="L1", active_seconds=12, source="restart",
                  remaining_lives=1, after=after_restart_loot,
                  flows=[dict(resource="coins", source="pickup", income=11, expense=0)])
        ])["groups"][0]

        runs = {run["run_id"]: run for run in group["runs"]}
        self.assertEqual(runs["r1"]["termination_reason"], "loss")
        self.assertEqual(runs["r1"]["result"], "loss")
        self.assertEqual(runs["r1"]["remaining_lives"], 0)
        self.assertEqual(runs["r1"]["loot"]["coins_income"], 25)

        self.assertEqual(runs["r2"]["termination_reason"], "win")
        self.assertEqual(runs["r2"]["result"], "win")
        self.assertEqual(runs["r2"]["remaining_lives"], 2)
        self.assertEqual(runs["r2"]["loot"]["coins_income"], 40)
        self.assertEqual(runs["r2"]["loot_sources"]["pickup"]["coins_income"], 30)
        self.assertEqual(runs["r2"]["loot_sources"]["obstacle_bonus"]["coins_income"], 10)
        self.assertEqual(runs["r2"]["expenses"]["crystals"], 1)
        self.assertEqual(runs["r2"]["expense_sources"]["RunRevivePurchased"]["crystals"], 1)

        self.assertEqual(runs["r3"]["termination_reason"], "restart")
        self.assertEqual(runs["r3"]["result"], "abandoned")
        self.assertEqual(runs["r3"]["remaining_lives"], 1)
        self.assertEqual(runs["r3"]["loot"]["coins_income"], 11)
        self.assertEqual(group["final_state"]["coins"], 65)

    def test_app_closed_run_keeps_reason_lives_and_unconfirmed_loot(self):
        start = snapshot(coins=10, crystals=1)
        after_close = snapshot(coins=27, crystals=1)
        group = self.summarize([
            event(1, "session_started", after=start),
            event(2, "run_started", run_id="r-close", level="L2", before=start),
            event(3, "run_finished", run_id="r-close", level="L2", active_seconds=18, source="app_closed",
                  remaining_lives=1, after=after_close,
                  flows=[dict(resource="coins", source="pickup", income=17, expense=0)])
        ])["groups"][0]

        run = group["runs"][0]
        self.assertEqual(run["termination_reason"], "app_closed")
        self.assertEqual(run["result"], "abandoned")
        self.assertEqual(run["remaining_lives"], 1)
        self.assertEqual(run["loot"]["coins_income"], 17)
        self.assertEqual(group["final_state"]["coins"], 10)


if __name__ == "__main__":
    unittest.main()
