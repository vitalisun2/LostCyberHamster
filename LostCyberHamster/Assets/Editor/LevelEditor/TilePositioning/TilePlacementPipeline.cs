using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilePlacementPipeline : ITilePlacementStrategy
{
    private readonly List<ITilePlacementRule> _rules = new();

    public void AddRule(ITilePlacementRule rule)
    {
        _rules.Add(rule);
    }

    public bool TryPlaceTile(Tilemap tilemap, Tile tile, Vector3 initialPos, out Vector3 finalPos)
    {
        finalPos = initialPos;

        foreach (var rule in _rules)
        {
            if (!rule.TryApplyRule(tilemap, tile, ref finalPos))
            {
                return false;
            }
        }

        return true;
    }
}
