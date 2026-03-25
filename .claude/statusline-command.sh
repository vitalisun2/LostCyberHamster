#!/usr/bin/env bash
# Claude Code status line: shows model name, context window usage,
# and remaining % for 5-hour and 7-day rate limit windows.
# Uses only bash + grep + sed — no jq required.

input=$(cat)

extract() {
  echo "$input" | grep -o "\"$1\":[^,}]*" | head -1 | grep -o '[0-9.]*\|"[^"]*"' | tail -1 | tr -d '"'
}

model=$(extract "id")
ctx_used=$(extract "used_percentage" | head -1)
five_used=$(echo "$input" | grep -o '"five_hour":{[^}]*}' | grep -o '"used_percentage":[0-9.]*' | grep -o '[0-9.]*')
week_used=$(echo "$input" | grep -o '"seven_day":{[^}]*}' | grep -o '"used_percentage":[0-9.]*' | grep -o '[0-9.]*')

out=""

if [ -n "$model" ]; then
  out="$model"
fi

if [ -n "$ctx_used" ]; then
  ctx_pct=$(awk "BEGIN {printf \"%.0f\", $ctx_used}")
  [ -n "$out" ] && out="$out | "
  out="${out}ctx: ${ctx_pct}%"
fi

if [ -n "$five_used" ]; then
  five_remaining=$(awk "BEGIN {printf \"%.0f\", 100 - $five_used}")
  [ -n "$out" ] && out="$out | "
  out="${out}5h: ${five_remaining}% left"
fi

if [ -n "$week_used" ]; then
  week_remaining=$(awk "BEGIN {printf \"%.0f\", 100 - $week_used}")
  [ -n "$out" ] && out="$out | "
  out="${out}7d: ${week_remaining}% left"
fi

echo "$out"
