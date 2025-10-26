using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Assets.Scripts.System
{
    public class LevelData
    {
        public LevelInfo LevelInfo { get; set; }

        // neccesary level references
        public Hamster Hamster { get; set; }
        public GameManager GameManager { get; set; }

        // availalable loaded addressables

        // sky sprites
        public GameObject SkyPrefab { get; set; }
        public Sprite SkySprite { get; set; }

        // background sprites
        public GameObject BackgroundPrefab { get; set; }
        public Sprite BackgroundSprite { get; set; }

        // road sprites
        public GameObject RoadPrefab { get; set; }
        public Sprite RoadSprite { get; set; }

    public AddressableSetLease<Sprite> ObstaclesSpritesLease { get; set; }
    public AddressableSetLease<Sprite> CollectablesSpritesLease { get; set; }
    public AddressableSetLease<Sprite> DecorSpritesLease { get; set; }

        // bonuses
        public CoinOneBonus CoinOneBonusPrefab { get; set; }
        public PizzaBonus PizzaBonusPrefab { get; set; }
        public EnergeticBonus EnergeticBonusPrefab { get; set; }
        public CrystalBonus CrystalBonusPrefab { get; set; }
        public LifeBonus LifeBonusPrefab { get; set; }

        // effects
        public BoomEffect BoomEffectPrefab { get; set; }

        // obstacles
        public GameObject SmallCitizenPrefab { get; set; }
        public GameObject BigCitizenPrefab { get; set; }
        public GameObject BigNotAlivePrefab { get; set; }

        // obstacles sprites
        public List<Sprite> ObstaclesSprites { get; set; } = new();

        // collectables sprites
        public List<Sprite> CollectablesSprites { get; set; } = new();

        // decor sprites
        public List<Sprite> DecorSprites { get; set; } = new();

        // intro sprites
        public List<Sprite> IntroSprites { get; set; } = new();
        public GameObject IntroObject { get; set; }
        public Sprite SkipButtonSprite { get; set; }
    }
}
