---
type: "query"
date: "2026-09-19T18:06:28.704108+00:00"
question: "Почему ReturnActivities ломается после Menu-Level-Menu и какой минимальный lifetime fix нужен?"
contributor: "graphify"
outcome: "useful"
source_nodes: ["ReturnActivitiesScreenController", "AddressableLease", "PreparedScreen"]
---

# Q: Почему ReturnActivities ломается после Menu-Level-Menu и какой минимальный lifetime fix нужен?

## Answer

Expanded from original query via vocab: [return, activities, addressable, lease, style, screen, prepared, resource, bundle, font, sprite]. Проверено по исходникам: Screen.uxml подключает common.uss в Menu и Game; common.uss импортирует стили остальных основных экранов, но не ReturnActivitiesScreen.uss. ReturnActivitiesScreen.uss принадлежит только VisualTreeAsset lease PreparedScreen и теряет Unity resource objects при release/unload; новый USS получает destroyed refs через вероятное повторное использование статического ComputedStyle cache. Минимальный fix: включить ReturnActivitiesScreen.uss в common.uss, сохранить симметричный Dispose экрана, проверить Android flow; точный cache-hit и последний handle остаются недоказанными.

## Outcome

- Signal: useful

## Source Nodes

- ReturnActivitiesScreenController
- AddressableLease
- PreparedScreen