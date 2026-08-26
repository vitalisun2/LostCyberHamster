using Assets.Scripts.System;
using System.IO;
using System;
using UnityEngine;

namespace Assets.Scripts
{
    public static class Consts
    {
        public const string PlayerPrefsKey = "GameData";
        public const string RemoteServerUrlBase = "http://localhost:5298";
        public const string GameId = "c5c541fe-414a-4cb6-b979-d68795096998";
        public const string PlayerDataUrl = "games/c5c541fe-414a-4cb6-b979-d68795096998/players/current";
        public const string AuthKey = "AuthKey";

        // Unity Leaderboards
        public const string NewYorkMorningLeaderboardId = "weekly-01-new-york-morning";
        public const string NewYorkAfternoonLeaderboardId = "weekly-01-new-york-afternoon";
        public const string NewYorkEveningLeaderboardId = "weekly-01-new-york-evening";
        public const string NewYorkNightLeaderboardId = "weekly-01-new-york-night";
        public const string ParisMorningLeaderboardId = "weekly-02-paris-morning";
        public const string ParisAfternoonLeaderboardId = "weekly-02-paris-afternoon";
        public const string ParisEveningLeaderboardId = "weekly-02-paris-evening";
        public const string ParisNightLeaderboardId = "weekly-02-paris-night";
        public const string BarcelonaMorningLeaderboardId = "weekly-03-barcelona-morning";
        public const string BarcelonaAfternoonLeaderboardId = "weekly-03-barcelona-afternoon";
        public const string BarcelonaEveningLeaderboardId = "weekly-03-barcelona-evening";
        public const string BarcelonaNightLeaderboardId = "weekly-03-barcelona-night";

        /// <summary>
        /// Идентификатор игры для платформы iOS
        /// </summary>
        public static string IOS_GAME_ID => "5724652";
        /// <summary>
        /// Идентификатор игры для платформы Android
        /// </summary>
        public static string ANDROID_GAME_ID => "5724653";

#if UNITY_EDITOR
        public static string BaseUrl => $"file://{Application.dataPath}/AssetBundles/{UserSettings.GetPlatformFolder()}";
#else
    public static string BaseUrl => $"https://games.vues.dev/lost-cyber-hamster/assets/{UserSettings.GetPlatformFolder()}";
#endif

#if UNITY_EDITOR
        public static string BaseSettingsPath => Path.Combine(Application.dataPath, "GameSettings");
#else
    public static string BaseSettingsPath => Path.Combine(Application.persistentDataPath, "GameSettings");
#endif

        [Obsolete]
        public const string BackgroundPrefabPath = "Assets/Core/Components/GameManager/Prefabs/BackgroundPrefab.prefab";
        public const string TilemapPrefabPath = "Assets/Core/Components/GameManager/Prefabs/TilemapPrefab.prefab";
        public const string TileDirectoryPath = "Assets/Core/Components/GameManager/Sprites/tiles";
        public const string TemplatesFallbackLocation = "01_New_York";

#if UNITY_EDITOR
        [Obsolete]
        public static string ApplicationResourcesPath = Path.Combine(Application.persistentDataPath, "Resources");

        /// <summary>
        /// Путь к папке с данными о локациях
        /// </summary>
        public static string LocationsPath = Path.Combine("Assets", "Content", "Locations");

        public static string TemplatesLocationName = "level_design_templates";

        /*  путь до общих коллектиблов  */
        public static string SharedSpritesPath = Path.Combine("Assets", "Content", "shared", "sprites");

        public static string BasePath => $"Assets/AssetBundles/{UserSettings.GetPlatformFolder()}";
#endif


        public const int FPS = 60;
        /// <summary>
        /// Основная скорость мира (юнитов в секунду),
        /// с которой смещаются фон и препятствия.
        /// </summary>
        public const float GameSpeedBase = 3.8f;

        public const float SkyScrollSpeed = 0.2f;
        public const float Background2ScrollSpeed = 0.5f;
        public const float BackgroundScrollSpeed = 0.7f;
        public const float RoadScrollSpeed = 1f;
        
        public const float BackgroundBottomYPos = -0.685f;
        public const float Background2BottomYPos = BackgroundBottomYPos + 0.885f;
        public const float SkyBottomYPos = BackgroundBottomYPos + (ENVIRONMENT_REFERENCE_HEIGHT * PIXELS_TO_UNITS_RATIO / 1.8f);
        public const float RoadBottomYPos = BackgroundBottomYPos - (ROAD_HEIGHT * PIXELS_TO_UNITS_RATIO);

        public const float CameraSize = 3.1f;
        public static Vector3 CameraPosition = new(0, 0, -10);

        public const float HamsterXPos = -3.78f;
        public const float HamsterYPos = -1.25f;

        /// <summary>Ширина большого неживого препятствия в юнитах.</summary>
        public static readonly float BIG_NOTALIVE_WIDTH_UNITS = BIG_NOTALIVE_WIDTH * PIXELS_TO_UNITS_RATIO;
        /// <summary>Высота большого неживого препятствия в юнитах.</summary>
        public static readonly float BIG_NOTALIVE_HEIGHT_UNITS = BIG_NOTALIVE_HEIGHT * PIXELS_TO_UNITS_RATIO;

        /// <summary>Ширина среднего неживого препятствия в юнитах.</summary>
        public static readonly float MEDIUM_NOTALIVE_WIDTH_UNITS = MEDIUM_NOTALIVE_WIDTH * PIXELS_TO_UNITS_RATIO;
        /// <summary>Высота среднего неживого препятствия в юнитах.</summary>
        public static readonly float MEDIUM_NOTALIVE_HEIGHT_UNITS = MEDIUM_NOTALIVE_HEIGHT * PIXELS_TO_UNITS_RATIO;

        /// <summary>Начальная Y-позиция transform_roof_run для большой крыши.</summary>
        public const float BIG_ROOF_RUN_START_Y = 1.55f;

        /// <summary>Начальная Y-позиция transform_medium_roof_run для средней крыши.</summary>
        public const float MEDIUM_ROOF_RUN_START_Y = 1.2616279f;


        /// <summary>
        /// Y-позиция верхнего края дороги (необходима для размещения декора выше дороги).
        /// </summary>
        public const float RoadUpperEdgeYPos = -0.65f;
        /// <summary>
        /// Y-позиция препятствий, которые находятся на верхней части дороги.
        /// </summary>
        public const float ObstacleY0Pos = -1.8f;
        /// <summary>
        /// Y-позиция препятствий, которые находятся на нижней части дороги.
        /// </summary>
        public const float ObstacleY1Pos = -2.8f;
        /// <summary>
        /// Смещение по Y для препятствий на крыше препятствий.
        /// </summary>
        public const float RoofOffset = 0.1f;
        /// <summary>
        /// Y-позиция препятствий, которые находятся на крыше препятствий на верхней части дороги.
        /// </summary>
        public static readonly float ObstacleRoofY0Pos = ObstacleY0Pos + BIG_NOTALIVE_HEIGHT_UNITS + RoofOffset;
        /// <summary>
        /// Y-позиция препятствий, которые находятся на крыше препятствий на нижней части дороги.
        /// </summary>
        public static readonly float ObstacleRoofY1Pos = ObstacleY1Pos + BIG_NOTALIVE_HEIGHT_UNITS + RoofOffset;

        public const float ObstacleLineTolerance = 0.12f;
        public const float BonusYOffset = 0.18f;
        public const float BigNotAliveEdgeTolerance = 0.5f;
        public const float BigOrSmallAliveEdgeTolerance = 0.2f;
        public const float SmallNotAliveEdgeTolerance = 0.2f;

        public const float BlinkTime = 0.2f;
        public const int BlinkNumber = 3;

        /// <summary>
        /// Доля ширины хомяка, которая задает максимальный passive gap между roof-platforms.
        /// </summary>
        public const float RoofRunPassiveContinuationGapFactor = 0.7f;

        /// <summary>
        /// Возвращает максимальный gap между roof-platforms для непрерывного RoofRun без input.
        /// </summary>
        public static float GetRoofRunPassiveContinuationGap(float hamsterWidthInUnits)
        {
            return hamsterWidthInUnits * RoofRunPassiveContinuationGapFactor;
        }

        public const float JumpOverObstacleRangeMin = 0;
        public const float JumpOverObstacleRangeMax = 2.2f;

        /// <summary>
        /// Базовый PPU, заданный в Import Settings всех спрайтов (см. Texture → Pixels Per Unit).
        /// </summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>
        /// Коэффициент «пиксели → юниты» (1 / PPU).
        /// </summary>
        public const float PIXELS_TO_UNITS_RATIO = 1f / PixelsPerUnit;   // 0.01

        /// <summary>
        /// Имя префаба для прокручиваемых слоёв окружения (фон, дорога и т.д.)
        /// </summary>
        public const string ScrollingEnvironmentPrefabName = "ScrollingEnvironmentPrefab";

        // Bonuses
        public const string CoinOneBonusPrefabName = "CoinOnePrefab";
        public const string CrystalBonusPrefabName = "CrystalBonusPrefab";
        public const string EnergeticBonusPrefabName = "EnergeticBonusPrefab";
        public const string LifeBonusPrefabName = "LifeBonusPrefab";
        public const string PizzaBonusPrefabName = "PizzaBonusPrefab";

        // Effects
        public const string BoomEffectPrefabName = "BoomPrefab";

        // Obstacles
        public const string SmallCitizenPrefabName = "SmallCitizenPrefab";
        public const string BigCitizenPrefabName = "BigCitizenPrefab";
        public const string MediumOrBigNotAlivePrefabName = "MediumOrBigNotAlivePrefab";

        // Labels for sprites
        public const string ObstaclesSpritesLabelPostfix = "obstacles sprites";
        public const string DecorSpritesLabelPostFix = "decor sprites";
        public const string CollectableSpritesLabel = "collectable sprites";
        public const string ObstacleAnimationsLabelPostfix = "obstacle animations";

        public const string Locations = "locations";
        public const string LevelsDaypart = "levels_daypart";

        public const float GapBetweenTiles = 0.2f;
        public const float GridSnapStep = 0.2f;
        /// <summary>
        /// Расстояние между визуальными краями соседних obstacle-паттернов.
        /// </summary>
        public const float PatternEdgeGap = 5f;

        // -------------------------
        // Новые константы размеров, значения в пикселях
        // -------------------------
        public const int BACKGROUND_WIDTH = 2000;
        public const int ENVIRONMENT_REFERENCE_HEIGHT = 240;

        public const int ROAD_WIDTH = 2000;
        public const int ROAD_HEIGHT = 240;

        public const int BONUS_WIDTH = 80;
        public const int BONUS_HEIGHT = 80;

        public const int HAMSTER_WIDTH = 326;
        public const int HAMSTER_HEIGHT = 84;

        public const int SMALL_ALIVE_WIDTH = 152;
        public const int SMALL_ALIVE_HEIGHT = 108;

        public const int BIG_ALIVE_WIDTH = 100;
        public const int BIG_ALIVE_HEIGHT = 212;

        public const int SMALL_NOTALIVE_WIDTH = 140;
        public const int SMALL_NOTALIVE_HEIGHT = 108;

        public const int BIG_NOTALIVE_WIDTH = 388;
        public const int BIG_NOTALIVE_HEIGHT = 172;

        public const int MEDIUM_NOTALIVE_WIDTH = 344;
        public const int MEDIUM_NOTALIVE_HEIGHT = 140;
    }

}
