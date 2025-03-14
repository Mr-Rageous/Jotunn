﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.GUI;
using Jotunn.Managers;
using Jotunn.Utils;
using TestMod.ConsoleCommands;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TestMod
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    [HarmonyPatch]
    internal class TestMod : BaseUnityPlugin
    {
        private const string ModGUID = "com.jotunn.testmod";
        private const string ModName = "Jotunn Test Mod #1";
        private const string ModVersion = "0.1.0";
        private const string JotunnTestModConfigSection = "JotunnTest";

        private Sprite TestSprite;
        private Texture2D TestTex;

        private AssetBundle BlueprintRuneBundle;
        private AssetBundle TestAssets;
        private AssetBundle Steelingot;

        private System.Version CurrentVersion;

        private ButtonConfig CreateColorPickerButton;
        private ButtonConfig CreateGradientPickerButton;
        private ButtonConfig TogglePanelButton;
        private GameObject TestPanel;

        private ButtonConfig RaiseSkillButton;
        private Skills.SkillType TestSkill;

        private ConfigEntry<KeyCode> EvilSwordSpecialConfig;
        private ConfigEntry<InputManager.GamepadButton> EvilSwordGamepadConfig;
        private ButtonConfig EvilSwordSpecialButton;
        private CustomStatusEffect EvilSwordEffect;

        private ConfigEntry<KeyCode> ServerKeyCodeConfig;
        private ButtonConfig ServerKeyCodeButton;
        private ConfigEntry<KeyboardShortcut> ServerShortcutConfig;
        private ButtonConfig ServerShortcutButton;

        private ConfigEntry<bool> EnableVersionMismatch;
        private static ConfigEntry<bool> EnableExtVersionMismatch;

        private CustomLocalization Localization;

        private static Harmony harmony;

        // Load, create and init your custom mod stuff
        private void Awake()
        {
            harmony = new Harmony("com.jotunn.testmod");
            harmony.PatchAll();

            // Show DateTime on Logs
            //Jotunn.Logger.ShowDate = true;

            // Create a custom Localization and add it to the Manager
            Localization = new CustomLocalization();
            LocalizationManager.Instance.AddLocalization(Localization);

            // Create stuff
            CreateConfigValues();
            LoadAssets();
            AddInputs();
            AddLocalizations();
            AddCommands();
            AddSkills();
            AddRecipes();
            AddStatusEffects();
            AddVanillaItemConversions();
            AddCustomItemConversion();
            AddCustomPrefabs();
            AddItemsWithConfigs();
            AddMockedItems();
            AddKitbashedPieces();
            AddPieceCategories();
            AddInvalidEntities();
            AddConePiece();

            // Add custom items cloned from vanilla items
            PrefabManager.OnVanillaPrefabsAvailable += AddClonedItems;

            // Add custom pieces cloned from vanilla pieces
            PrefabManager.OnVanillaPrefabsAvailable += CreateDeerRugPiece;

            // Clone an item with variants and replace them
            PrefabManager.OnVanillaPrefabsAvailable += AddVariants;

            // Create a custom item with variants
            PrefabManager.OnVanillaPrefabsAvailable += AddCustomVariants;

            // Create a custom item with rendered icons
            PrefabManager.OnVanillaPrefabsAvailable += AddItemsWithRenderedIcons;

            // Create custom locations and vegetation
            PrefabManager.OnVanillaPrefabsAvailable += AddCustomLocationsAndVegetation;
            ZoneManager.OnVanillaLocationsAvailable += AddClonedVanillaLocationsAndVegetations;
            ZoneManager.OnVanillaLocationsAvailable += ModifyVanillaLocationsAndVegetation;

            // Create custom creatures and spawns
            AddCustomCreaturesAndSpawns();
            // Hook creature manager to get access to vanilla creature prefabs
            CreatureManager.OnVanillaCreaturesAvailable += ModifyAndCloneVanillaCreatures;

            // Test config sync event
            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    Jotunn.Logger.LogMessage("Initial Config sync event received");
                }
                else
                {
                    Jotunn.Logger.LogMessage("Config sync event received");
                }
            };

            // Test admin status sync event
            SynchronizationManager.OnAdminStatusChanged += () =>
            {
                Jotunn.Logger.LogMessage($"Admin status sync event received: {(SynchronizationManager.Instance.PlayerIsAdmin ? "Youre admin now" : "Downvoted, boy")}");
            };


            // Get current version for the mod compatibility test
            CurrentVersion = new System.Version(Info.Metadata.Version.ToString());
            SetVersion();
        }

        // Called every frame
        private void Update()
        {
            // Since our Update function in our BepInEx mod class will load BEFORE Valheim loads,
            // we need to check that ZInput is ready to use first.
            if (ZInput.instance != null)
            {
                // Check if custom buttons are pressed.
                // Custom buttons need to be added to the InputManager before we can poll them.
                // GetButtonDown will only return true ONCE, right after our button is pressed.
                // If we hold the button down, it won't spam toggle our menu.
                if (ZInput.GetButtonDown(TogglePanelButton.Name))
                {
                    TogglePanel();
                }

                // Show ColorPicker or GradientPicker via GUIManager
                if (ZInput.GetButtonDown(CreateColorPickerButton.Name))
                {
                    CreateColorPicker();
                }
                if (ZInput.GetButtonDown(CreateGradientPickerButton.Name))
                {
                    CreateGradientPicker();
                }

                // Raise the test skill
                if (Player.m_localPlayer != null && ZInput.GetButtonDown(RaiseSkillButton.Name))
                {
                    Player.m_localPlayer.RaiseSkill(TestSkill, 1f);
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Swords, 1f);
                }

                // Use the name of the ButtonConfig to identify the button pressed
                if (EvilSwordSpecialButton != null && MessageHud.instance != null &&
                    Player.m_localPlayer != null && Player.m_localPlayer.m_visEquipment.m_rightItem == "EvilSword")
                {
                    if (ZInput.GetButton(EvilSwordSpecialButton.Name) && MessageHud.instance.m_msgQeue.Count == 0)
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$evilsword_beevilmessage");
                    }
                }

                // Use the name of the ButtonConfig to identify the button pressed
                if (ServerKeyCodeButton != null && MessageHud.instance != null)
                {
                    if (ZInput.GetButtonDown(ServerKeyCodeButton.Name) && MessageHud.instance.m_msgQeue.Count == 0)
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Server KeyCode pressed");
                    }
                }
                if (ServerShortcutButton != null && MessageHud.instance != null)
                {
                    if (ZInput.GetButtonDown(ServerShortcutButton.Name) && MessageHud.instance.m_msgQeue.Count == 0)
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Server Shortcut pressed");
                    }
                }

                // Spawn goblin with a variant sword
                if (Player.m_localPlayer != null && Input.GetKeyDown(KeyCode.J))
                {
                    var pre = PrefabManager.Instance.GetPrefab("GoblinArcher");
                    var go = Object.Instantiate(pre,
                        Player.m_localPlayer.transform.position + GameCamera.instance.transform.forward * 2f + Vector3.up,
                        Quaternion.Euler(GameCamera.instance.transform.forward));
                    Object.Destroy(go.GetComponent<MonsterAI>());

                    var rand = new Random();

                    int swordrand = rand.Next(0, 6);
                    if (swordrand < 4)
                    {
                        var sword = Object.Instantiate(PrefabManager.Instance.GetPrefab("item_swordvariants"))
                            .GetComponent<ItemDrop>().m_itemData;
                        sword.m_variant = swordrand;
                        go.GetComponent<Humanoid>().m_rightItem = sword;
                    }

                    int shieldrand = rand.Next(0, 10);
                    if (shieldrand < 4)
                    {
                        var shield = Object.Instantiate(PrefabManager.Instance.GetPrefab("item_lulvariants"))
                            .GetComponent<ItemDrop>().m_itemData;
                        shield.m_variant = shieldrand;
                        go.GetComponent<Humanoid>().m_leftItem = shield;
                    }

                    Jotunn.Logger.LogMessage($"Rolled sword {swordrand} shield {shieldrand}");
                }
            }
        }

        // Toggle our test panel with button
        private void TogglePanel()
        {
            // Create the panel if it does not exist
            if (!TestPanel)
            {
                if (GUIManager.Instance == null)
                {
                    Logger.LogError("GUIManager instance is null");
                    return;
                }

                if (!GUIManager.CustomGUIFront)
                {
                    Logger.LogError("GUIManager CustomGUI is null");
                    return;
                }

                // Create the panel object
                TestPanel = GUIManager.Instance.CreateWoodpanel(
                    parent: GUIManager.CustomGUIFront.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, 0),
                    width: 850,
                    height: 600,
                    draggable: false);
                TestPanel.SetActive(false);

                // Add the Jötunn draggable Component to the panel
                // Note: This is normally automatically added when using CreateWoodpanel()
                TestPanel.AddComponent<DragWindowCntrl>();

                // Create the text object
                GUIManager.Instance.CreateText(
                    text: "Jötunn, the Valheim Lib",
                    parent: TestPanel.transform,
                    anchorMin: new Vector2(0.5f, 1f),
                    anchorMax: new Vector2(0.5f, 1f),
                    position: new Vector2(0f, -50f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 30,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 350f,
                    height: 40f,
                    addContentSizeFitter: false);

                // Create the button object
                GameObject buttonObject = GUIManager.Instance.CreateButton(
                    text: "A Test Button - long dong schlongsen text",
                    parent: TestPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, -250f),
                    width: 250f,
                    height: 60f);
                buttonObject.SetActive(true);

                // Add a listener to the button to close the panel again
                Button button = buttonObject.GetComponent<Button>();
                button.onClick.AddListener(TogglePanel);

                // Create a dropdown
                var dropdownObject = GUIManager.Instance.CreateDropDown(
                    parent: TestPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-250f, -250f),
                    fontSize: 16,
                    width: 160f,
                    height: 30f);
                dropdownObject.GetComponent<Dropdown>().AddOptions(new List<string>
                {
                    "bla", "blubb", "börks", "blarp", "harhar"
                });

                // Create an input field
                GUIManager.Instance.CreateInputField(
                    parent: TestPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(250f, -250f),
                    contentType: InputField.ContentType.Standard,
                    placeholderText: "input...",
                    fontSize: 16,
                    width: 160f,
                    height: 30f);

                // Create PluginInfo
                GameObject scrollView = GUIManager.Instance.CreateScrollView(
                    TestPanel.transform,
                    false, true, 10f, 5f,
                    GUIManager.Instance.ValheimScrollbarHandleColorBlock, Color.black,
                    700f, 400f);

                RectTransform viewport =
                    scrollView.transform.Find("Scroll View/Viewport/Content") as RectTransform;

                foreach (var mod in ModRegistry.GetMods(true).OrderBy(x => x.GUID))
                {
                    // Mod GUID
                    GUIManager.Instance.CreateText(mod.GUID,
                        viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                        GUIManager.Instance.AveriaSerifBold, 30, GUIManager.Instance.ValheimOrange,
                        true, Color.black, 650f, 40f, false);

                    if (mod.Pieces.Any())
                    {
                        // Pieces title
                        GUIManager.Instance.CreateText("Pieces:",
                            viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                            GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange,
                            true, Color.black, 650f, 30f, false);

                        foreach (var piece in mod.Pieces)
                        {
                            // Piece name
                            GUIManager.Instance.CreateText($"{piece}",
                                viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                                GUIManager.Instance.AveriaSerifBold, 16, Color.white,
                                true, Color.black, 650f, 30f, false);
                        }
                    }

                    if (mod.Items.Any())
                    {
                        // Items title
                        GUIManager.Instance.CreateText("Items:",
                            viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                            GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange,
                            true, Color.black, 650f, 30f, false);

                        foreach (var item in mod.Items)
                        {
                            // Piece name
                            GUIManager.Instance.CreateText($"{item}",
                                viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                                GUIManager.Instance.AveriaSerifBold, 16, Color.white,
                                true, Color.black, 650f, 30f, false);
                        }
                    }

                    if (mod.Commands.Any())
                    {
                        // Commands title
                        GUIManager.Instance.CreateText("Commands:",
                            viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                            GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange,
                            true, Color.black, 650f, 30f, false);

                        foreach (var command in mod.Commands)
                        {
                            // Command name
                            GUIManager.Instance.CreateText($"{command.Name}: {command.Help}",
                                viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                                GUIManager.Instance.AveriaSerifBold, 16, Color.white,
                                true, Color.black, 650f, 30f, false);
                        }
                    }

                    if (mod.Translations.Any())
                    {
                        // Translations title
                        GUIManager.Instance.CreateText("Translations:",
                            viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                            GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange,
                            true, Color.black, 650f, 30f, false);

                        foreach (var translation in mod.Translations)
                        {
                            foreach (var tokenvalue in translation.GetTranslations(
                                PlayerPrefs.GetString("language", "English")))
                            {
                                // Token - Value
                                GUIManager.Instance.CreateText($"{tokenvalue.Key}: {tokenvalue.Value}",
                                    viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                                    GUIManager.Instance.AveriaSerifBold, 16, Color.white,
                                    true, Color.black, 650f, 30f, false);
                            }
                        }
                    }
                }
            }

            // Switch the current state
            bool state = !TestPanel.activeSelf;

            // Set the active state of the panel
            TestPanel.SetActive(state);

            // Toggle input for the player and camera while displaying the GUI
            GUIManager.BlockInput(state);
        }

        // Create a new ColorPicker when hovering a piece
        private void CreateColorPicker()
        {
            if (GUIManager.Instance == null)
            {
                Jotunn.Logger.LogError("GUIManager instance is null");
                return;
            }

            if (SceneManager.GetActiveScene().name == "start")
            {
                GUIManager.Instance.CreateColorPicker(
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0),
                    GUIManager.Instance.ValheimOrange, "Choose your poison", null, null, true);
            }

            if (SceneManager.GetActiveScene().name == "main" && ColorPicker.done)
            {
                var hovered = Player.m_localPlayer.GetHoverObject();
                var current = hovered?.GetComponentInChildren<Renderer>();
                if (current != null)
                {
                    current.gameObject.AddComponent<ColorChanger>();
                }
                else
                {
                    var parent = hovered?.transform.parent.gameObject.GetComponentInChildren<Renderer>();
                    if (parent != null)
                    {
                        parent.gameObject.AddComponent<ColorChanger>();
                    }
                }
            }

        }

        // Create a new GradientPicker
        private void CreateGradientPicker()
        {
            if (GUIManager.Instance == null)
            {
                Jotunn.Logger.LogError("GUIManager instance is null");
                return;
            }

            if (SceneManager.GetActiveScene().name == "start")
            {
                GUIManager.Instance.CreateGradientPicker(
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0),
                    new Gradient(), "Gradiwut?", null, null);
            }

            if (SceneManager.GetActiveScene().name == "main" && GradientPicker.done)
            {
                var hovered = Player.m_localPlayer.GetHoverObject();
                var current = hovered?.GetComponentInChildren<Renderer>();
                if (current != null)
                {
                    current.gameObject.AddComponent<GradientChanger>();
                }
                else
                {
                    var parent = hovered?.transform.parent.gameObject.GetComponentInChildren<Renderer>();
                    if (parent != null)
                    {
                        parent.gameObject.AddComponent<GradientChanger>();
                    }
                }
            }
        }

        // Create persistent configurations for the mod
        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            // Add server config which gets pushed to all clients connecting and can only be edited by admins
            // In local/single player games the player is always considered the admin
            Config.Bind(JotunnTestModConfigSection, "Server String", "StringValue",
                new ConfigDescription("Server side string", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind(JotunnTestModConfigSection, "Server Float", 750f,
                new ConfigDescription("Server side float",
                    new AcceptableValueRange<float>(500f, 1000f),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind(JotunnTestModConfigSection, "Server Double", 20d,
                new ConfigDescription("Server side double",
                    new AcceptableValueRange<double>(5d, 25d),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind(JotunnTestModConfigSection, "Server Integer", 200,
                new ConfigDescription("Server side integer",
                    new AcceptableValueRange<int>(5, 25),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind(JotunnTestModConfigSection, "Server Color", new Color(0f, 1f, 0f, 1f),
                new ConfigDescription("Server side Color", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind(JotunnTestModConfigSection, "Server Integer Range", 7,
                        new ConfigDescription("Server Integer Range", new AcceptableValueRange<int>(5, 15),
                                              new ConfigurationManagerAttributes() { IsAdminOnly = true }));

            // Keys
            ServerKeyCodeConfig =
                Config.Bind(JotunnTestModConfigSection, "Server KeyCode", KeyCode.Alpha0,
                    new ConfigDescription("Server side KeyCode", null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ServerShortcutConfig =
                Config.Bind(JotunnTestModConfigSection, "Server KeyboardShortcut",
                    new KeyboardShortcut(KeyCode.A, KeyCode.LeftControl),
                    new ConfigDescription("Testing how KeyboardShortcut behaves", null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Test colored text configs
            Config.Bind(JotunnTestModConfigSection, "Server Bool", false,
                new ConfigDescription("Server side bool", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true, EntryColor = Color.blue, DescriptionColor = Color.yellow }));

            // Test invisible configs
            Config.Bind(JotunnTestModConfigSection, "InvisibleInt", 150,
                new ConfigDescription("Invisible int, testing browsable=false", null,
                    new ConfigurationManagerAttributes() { Browsable = false }));

            // Test ReadOnly
            Config.Bind(JotunnTestModConfigSection, "ReadOnlyInt", 7,
                        new ConfigDescription("ReadOnly int, testing ReadOnly=true", new AcceptableValueRange<int>(5, 15),
                                              new ConfigurationManagerAttributes() { ReadOnly = true }));

            // Add client config to test ModCompatibility
            EnableVersionMismatch = Config.Bind(JotunnTestModConfigSection, nameof(EnableVersionMismatch), false,
                new ConfigDescription("Enable to test ModCompatibility module"));
            EnableExtVersionMismatch = Config.Bind(JotunnTestModConfigSection, nameof(EnableExtVersionMismatch), false,
                new ConfigDescription("Enable to test external version mismatch"));
            Config.SettingChanged += Config_SettingChanged;

            // Add a client side custom input key for the EvilSword
            EvilSwordSpecialConfig = Config.Bind(JotunnTestModConfigSection, "EvilSwordSpecialAttack", KeyCode.B,
                new ConfigDescription("Key to unleash evil with the Evil Sword"));
            EvilSwordGamepadConfig = Config.Bind(JotunnTestModConfigSection, "EvilSwordSpecialAttackGamepad", InputManager.GamepadButton.ButtonSouth,
                new ConfigDescription("Button to unleash evil with the Evil Sword"));
        }

        // React on changed settings
        private void Config_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (e.ChangedSetting.Definition.Section == JotunnTestModConfigSection && e.ChangedSetting.Definition.Key == nameof(EnableVersionMismatch))
            {
                SetVersion();
            }
        }

        // Load assets
        private void LoadAssets()
        {
            // Load texture
            TestTex = AssetUtils.LoadTexture("TestMod/Assets/test_tex.jpg");
            TestSprite = Sprite.Create(TestTex, new Rect(0f, 0f, TestTex.width, TestTex.height), Vector2.zero);

            // Load asset bundle from filesystem
            TestAssets = AssetUtils.LoadAssetBundle("TestMod/Assets/jotunnlibtest");
            Jotunn.Logger.LogInfo(TestAssets);

            // Load asset bundle from filesystem
            BlueprintRuneBundle = AssetUtils.LoadAssetBundle("TestMod/Assets/testblueprints");
            Jotunn.Logger.LogInfo(BlueprintRuneBundle);

            // Load Steel ingot from streamed resource
            Steelingot = AssetUtils.LoadAssetBundleFromResources("steel");

            // Embedded Resources
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(TestMod).Assembly.GetManifestResourceNames())}");
        }

        // Add custom key bindings
        private void AddInputs()
        {
            // Add key bindings on the fly
            TogglePanelButton = new ButtonConfig { Name = "TestMod_Menu", Key = KeyCode.Home, ActiveInCustomGUI = true };
            InputManager.Instance.AddButton(ModGUID, TogglePanelButton);

            CreateColorPickerButton = new ButtonConfig { Name = "TestMod_Color", Key = KeyCode.PageUp };
            InputManager.Instance.AddButton(ModGUID, CreateColorPickerButton);
            CreateGradientPickerButton = new ButtonConfig { Name = "TestMod_Gradient", Key = KeyCode.PageDown };
            InputManager.Instance.AddButton(ModGUID, CreateGradientPickerButton);

            // Add key bindings backed by a config value
            // The HintToken is used for the custom KeyHint of the EvilSword
            EvilSwordSpecialButton = new ButtonConfig
            {
                Name = "EvilSwordSpecialAttack",
                Config = EvilSwordSpecialConfig,
                GamepadConfig = EvilSwordGamepadConfig,
                HintToken = "$evilsword_beevil",
                BlockOtherInputs = true
            };
            InputManager.Instance.AddButton(ModGUID, EvilSwordSpecialButton);

            // Add a key binding to test skill raising
            RaiseSkillButton = new ButtonConfig { Name = "TestMod_RaiseSkill", Key = KeyCode.Insert, ActiveInGUI = true, ActiveInCustomGUI = true };
            InputManager.Instance.AddButton(ModGUID, RaiseSkillButton);

            // Server config test keys
            ServerKeyCodeButton = new ButtonConfig { Name = "ServerKeyCode", Config = ServerKeyCodeConfig };
            InputManager.Instance.AddButton(ModGUID, ServerKeyCodeButton);
            ServerShortcutButton = new ButtonConfig { Name = "ServerShortcut", ShortcutConfig = ServerShortcutConfig };
            InputManager.Instance.AddButton(ModGUID, ServerShortcutButton);
        }

        // Adds localizations with configs
        private void AddLocalizations()
        {
            // Add translations for the custom item in AddClonedItems
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"item_evilsword", "Sword of Darkness"}, {"item_evilsword_desc", "Bringing the light"},
                {"evilsword_shwing", "Woooosh"}, {"evilsword_scroll", "*scroll*"},
                {"evilsword_beevil", "Be evil"}, {"evilsword_beevilmessage", ":reee:"},
                {"evilsword_effectname", "Evil"}, {"evilsword_effectstart", "You feel evil"},
                {"evilsword_effectstop", "You feel nice again"}
            });

            // Add translations for the custom piece in AddPieceCategories
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "piece_lul", "Lulz" }, { "piece_lul_description", "Do it for them" },
                { "piece_lel", "Lölz" }, { "piece_lel_description", "Härhärhär" }
            });

            // Add translations for the custom variant in AddClonedItems
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "lulz_shield", "Lulz Shield" }, { "lulz_shield_desc", "Lough at your enemies" }
            });

            // Add translations for the rendered tree
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"rendered_tree", "Rendered Tree"}, {"rendered_tree_desc", "A powerful tree, that can render its own icon. Magic!"}
            });
        }

        // Register new console commands
        private void AddCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new PrintItemsCommand());
            CommandManager.Instance.AddConsoleCommand(new TpCommand());
            CommandManager.Instance.AddConsoleCommand(new ListPlayersCommand());
            CommandManager.Instance.AddConsoleCommand(new SkinColorCommand());
            CommandManager.Instance.AddConsoleCommand(new BetterSpawnCommand());
            CommandManager.Instance.AddConsoleCommand(new CreateCategoryTabCommand());
            CommandManager.Instance.AddConsoleCommand(new RemoveCategoryTabCommand());
            CommandManager.Instance.AddConsoleCommand(new ResetCartographyCommand());
        }

        // Register new skills
        private void AddSkills()
        {
            // Test adding a skill with a texture
            TestSkill = SkillManager.Instance.AddSkill(new SkillConfig()
            {
                Identifier = "com.jotunn.testmod.testskill_code",
                Name = "Testing Skill From Code",
                Description = "A testing skill (but from code)!",
                Icon = TestSprite
            });

            // Test adding skills from JSON
            SkillManager.Instance.AddSkillsFromJson("TestMod/Assets/skills.json");
        }

        // Add custom recipes
        private void AddRecipes()
        {
            // Load recipes from JSON file
            ItemManager.Instance.AddRecipesFromJson("TestMod/Assets/recipes.json");

            var meatConfig = new RecipeConfig();
            meatConfig.Item = "CookedMeat"; // Name of the item prefab to be crafted
            meatConfig.AddRequirement(new RequirementConfig("Stone", 2));
            meatConfig.AddRequirement(new RequirementConfig("Wood", 1));
            ItemManager.Instance.AddRecipe(new CustomRecipe(meatConfig));
        }

        // Add new status effects
        private void AddStatusEffects()
        {
            // Create a new status effect. The base class "StatusEffect" does not do very much except displaying messages
            // A Status Effect is normally a subclass of StatusEffects which has methods for further coding of the effects (e.g. SE_Stats).
            StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
            effect.name = "EvilStatusEffect";
            effect.m_name = "$evilsword_effectname";
            effect.m_icon = AssetUtils.LoadSpriteFromFile("TestMod/Assets/reee.png");
            effect.m_startMessageType = MessageHud.MessageType.Center;
            effect.m_startMessage = "$evilsword_effectstart";
            effect.m_stopMessageType = MessageHud.MessageType.Center;
            effect.m_stopMessage = "$evilsword_effectstop";

            EvilSwordEffect = new CustomStatusEffect(effect, fixReference: false);  // We dont need to fix refs here, because no mocks were used
            ItemManager.Instance.AddStatusEffect(EvilSwordEffect);
        }

        // Add item conversions (cooking or smelter recipes)
        private void AddVanillaItemConversions()
        {
            // Add an item conversion for the CookingStation. The items must have an "attach" child GameObject to display it on the station.
            var cookConfig = new CookingConversionConfig();
            cookConfig.FromItem = "CookedMeat";
            cookConfig.ToItem = "CookedLoxMeat";
            cookConfig.CookTime = 2f;
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(cookConfig));

            // Add an item conversion for the Fermenter. You can specify how much new items the conversion yields.
            var fermentConfig = new FermenterConversionConfig();
            fermentConfig.ToItem = "CookedLoxMeat";
            fermentConfig.FromItem = "Coal";
            fermentConfig.ProducedItems = 10;
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(fermentConfig));

            // Add an item conversion for the smelter
            var smelterConfig = new SmelterConversionConfig();
            smelterConfig.FromItem = "Stone";
            smelterConfig.ToItem = "CookedLoxMeat";
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(smelterConfig));

            // Add an item conversion which does not resolve the mock
            var faultConfig = new SmelterConversionConfig();
            faultConfig.FromItem = "StonerDude";
            faultConfig.ToItem = "CookedLoxMeat";
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(faultConfig));

            // Add an incinerator conversion. This one is special since the incinerator conversion script 
            // takes one or more items to produce any amount of a new item
            var incineratorConfig = new IncineratorConversionConfig();
            incineratorConfig.Requirements.Add(new IncineratorRequirementConfig("Wood", 1));
            incineratorConfig.Requirements.Add(new IncineratorRequirementConfig("Stone", 1));
            incineratorConfig.ToItem = "Coins";
            incineratorConfig.ProducedItems = 20;
            incineratorConfig.RequireOnlyOneIngredient = false;  // true = only one of the requirements is needed to produce the output
            incineratorConfig.Priority = 5;                      // Higher priorities get preferred when multiple requirements are met
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(incineratorConfig));
        }

        // Add custom item conversion (gives a steel ingot to smelter)
        private void AddCustomItemConversion()
        {
            var steelPrefab = Steelingot.LoadAsset<GameObject>("Steel");
            var ingot = new CustomItem(steelPrefab, fixReference: false);
            ItemManager.Instance.AddItem(ingot);

            // Create a conversion for the blastfurnace, the custom item is the new outcome
            var blastConfig = new SmelterConversionConfig();
            blastConfig.Station = "blastfurnace"; // let's specify something other than default here
            blastConfig.FromItem = "Iron";
            blastConfig.ToItem = "Steel"; // this is our custom prefabs name we have loaded just above

            ItemManager.Instance.AddItemConversion(new CustomItemConversion(blastConfig));
            Steelingot.Unload(false);
        }

        // Add some custom prefabs
        private void AddCustomPrefabs()
        {
            // Dont fix references
            var noFix = PrefabManager.Instance.CreateEmptyPrefab("prefab_nofix", false);
            PrefabManager.Instance.AddPrefab(noFix);

            // Fix references
            var fix = PrefabManager.Instance.CreateEmptyPrefab("prefab_fix", false);
            fix.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
            fix.GetComponent<Renderer>().material.name = "JVLmock_amber";
            PrefabManager.Instance.AddPrefab(new CustomPrefab(fix, true));

            // Test duplicate
            PrefabManager.Instance.AddPrefab(fix);
        }

        // Add new Items with item Configs
        private void AddItemsWithConfigs()
        {
            // Add a custom piece table with custom categories
            PieceTableConfig runeTable = new PieceTableConfig();
            runeTable.CanRemovePieces = false;
            runeTable.UseCategories = false;
            runeTable.UseCustomCategories = true;
            runeTable.CustomCategories = new string[] { "Make", "Place" };
            PieceManager.Instance.AddPieceTable(new CustomPieceTable(BlueprintRuneBundle, "_BlueprintTestTable", runeTable));

            // Create and add a custom item
            ItemConfig runeConfig = new ItemConfig();
            runeConfig.Amount = 1;
            runeConfig.AddRequirement(new RequirementConfig("Stone", 1));
            // Prefab did not use mocked refs so no need to fix them
            var runeItem = new CustomItem(BlueprintRuneBundle, "BlueprintTestRune", fixReference: false, runeConfig);
            ItemManager.Instance.AddItem(runeItem);

            // Create and add custom pieces
            PieceConfig makeConfig = new PieceConfig();
            makeConfig.PieceTable = "_BlueprintTestTable";
            makeConfig.Category = "Make";
            var makePiece = new CustomPiece(BlueprintRuneBundle, "make_testblueprint", fixReference: false, makeConfig);
            PieceManager.Instance.AddPiece(makePiece);

            var placeConfig = new PieceConfig();
            placeConfig.PieceTable = "_BlueprintTestTable";
            placeConfig.Category = "Place";
            placeConfig.AllowedInDungeons = true;
            placeConfig.AddRequirement(new RequirementConfig("Wood", 2));
            var placePiece = new CustomPiece(BlueprintRuneBundle, "piece_testblueprint", fixReference: false, placeConfig);
            PieceManager.Instance.AddPiece(placePiece);

            // Add localizations from the asset bundle
            var textAssets = BlueprintRuneBundle.LoadAllAssets<TextAsset>();
            foreach (var textAsset in textAssets)
            {
                var lang = textAsset.name.Replace(".json", null);
                Localization.AddJsonFile(lang, textAsset.ToString());
            }

            // Override "default" KeyHint with an empty config
            KeyHintConfig KHC_base = new KeyHintConfig
            {
                Item = "BlueprintTestRune"
            };
            KeyHintManager.Instance.AddKeyHint(KHC_base);

            // Add custom KeyHints for specific pieces
            KeyHintConfig KHC_make = new KeyHintConfig
            {
                Item = "BlueprintTestRune",
                Piece = "make_testblueprint",
                ButtonConfigs = new[]
                {
                    // Override vanilla "Attack" key text
                    new ButtonConfig { Name = "Attack", HintToken = "$bprune_make" }
                }
            };
            KeyHintManager.Instance.AddKeyHint(KHC_make);

            KeyHintConfig KHC_piece = new KeyHintConfig
            {
                Item = "BlueprintTestRune",
                Piece = "piece_testblueprint",
                ButtonConfigs = new[]
                {
                    // Override vanilla "Attack" key text
                    new ButtonConfig { Name = "Attack", HintToken = "$bprune_piece" }
                }
            };
            KeyHintManager.Instance.AddKeyHint(KHC_piece);

            // Add additional localization manually
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "bprune_make", "Capture Blueprint"}, { "bprune_piece", "Place Blueprint"}
            });

            // Don't forget to unload the bundle to free the resources
            BlueprintRuneBundle.Unload(false);
        }

        // Add new items with mocked prefabs
        private void AddMockedItems()
        {
            // Load assets from resources
            var assetstream = typeof(TestMod).Assembly.GetManifestResourceStream("TestMod.AssetsEmbedded.capeironbackpack");
            if (assetstream == null)
            {
                Jotunn.Logger.LogWarning("Requested asset stream could not be found.");
            }
            else
            {
                var assetBundle = AssetBundle.LoadFromStream(assetstream);
                var prefab = assetBundle.LoadAsset<GameObject>("Assets/Evie/CapeIronBackpack.prefab");
                if (!prefab)
                {
                    Jotunn.Logger.LogWarning($"Failed to load asset from bundle: {assetBundle}");
                }
                else
                {
                    // Create and add a custom item
                    var CI = new CustomItem(prefab, fixReference: true);  // Mocked refs in prefabs need to be fixed
                    ItemManager.Instance.AddItem(CI);

                    // Create and add a custom recipe
                    var recipe = ScriptableObject.CreateInstance<Recipe>();
                    recipe.name = "Recipe_Backpack";
                    recipe.m_item = prefab.GetComponent<ItemDrop>();
                    recipe.m_craftingStation = Mock<CraftingStation>.Create("piece_workbench");
                    var ingredients = new List<Piece.Requirement>
                    {
                        MockRequirement.Create("LeatherScraps", 10),
                        MockRequirement.Create("DeerHide", 2),
                        MockRequirement.Create("Iron", 4)
                    };
                    recipe.m_resources = ingredients.ToArray();
                    var CR = new CustomRecipe(recipe, fixReference: true, fixRequirementReferences: true);  // Mocked main and requirement refs need to be fixed
                    ItemManager.Instance.AddRecipe(CR);

                    // Enable BoneReorder
                    BoneReorder.ApplyOnEquipmentChanged();
                }

                assetBundle.Unload(false);
            }

            // Load completely mocked "Shit Sword" (Cheat Sword copy)
            var cheatybundle = AssetUtils.LoadAssetBundleFromResources("cheatsword");
            var cheaty = cheatybundle.LoadAsset<GameObject>("Cheaty");
            ItemManager.Instance.AddItem(new CustomItem(cheaty, fixReference: true));
            cheatybundle.Unload(false);
        }

        private void AddConePiece()
        {
            AssetBundle pieceBundle = AssetUtils.LoadAssetBundleFromResources("pieces");

            PieceConfig cylinder = new PieceConfig();
            cylinder.Name = "$cylinder_display_name";
            cylinder.PieceTable = "Hammer";
            cylinder.AddRequirement(new RequirementConfig("Wood", 2, 0, true));

            PieceManager.Instance.AddPiece(new CustomPiece(pieceBundle, "Cylinder", fixReference: false, cylinder));
        }

        private void CreateDeerRugPiece()
        {
            PieceConfig rug = new PieceConfig();
            rug.Name = "$our_rug_deer_display_name";
            rug.PieceTable = "Hammer";
            rug.Category = "Misc";
            rug.AddRequirement(new RequirementConfig("Wood", 2, 0, true));

            PieceManager.Instance.AddPiece(new CustomPiece("our_rug_deer", "rug_deer", rug));

            // You want that to run only once, Jotunn has the piece cached for the game session
            PrefabManager.OnVanillaPrefabsAvailable -= CreateDeerRugPiece;
        }

        // Adds Kitbashed pieces
        private void AddKitbashedPieces()
        {
            // A simple kitbash piece, we will begin with the "empty" prefab as the base
            var simpleKitbashPiece = new CustomPiece("piece_simple_kitbash", true, "Hammer");
            simpleKitbashPiece.FixReference = true;
            simpleKitbashPiece.Piece.m_icon = TestSprite;
            PieceManager.Instance.AddPiece(simpleKitbashPiece);

            // Now apply our Kitbash to the piece
            KitbashManager.Instance.AddKitbash(simpleKitbashPiece.PiecePrefab, new KitbashConfig
            {
                Layer = "piece",
                KitbashSources = new List<KitbashSourceConfig>
                {
                    new KitbashSourceConfig
                    {
                        Name = "eye_1",
                        SourcePrefab = "Ruby",
                        SourcePath = "attach/model",
                        Position = new Vector3(0.528f, 0.1613345f, -0.253f),
                        Rotation = Quaternion.Euler(0, 180, 0f),
                        Scale = new Vector3(0.02473f, 0.05063999f, 0.05064f)
                    },
                    new KitbashSourceConfig
                    {
                        Name = "eye_2",
                        SourcePrefab = "Ruby",
                        SourcePath = "attach/model",
                        Position = new Vector3(0.528f, 0.1613345f, 0.253f),
                        Rotation = Quaternion.Euler(0, 180, 0f),
                        Scale = new Vector3(0.02473f, 0.05063999f, 0.05064f)
                    },
                    new KitbashSourceConfig
                    {
                        Name = "mouth",
                        SourcePrefab = "draugr_bow",
                        SourcePath = "attach/bow",
                        Position = new Vector3(0.53336f, -0.315f, -0.001953f),
                        Rotation = Quaternion.Euler(-0.06500001f, -2.213f, -272.086f),
                        Scale = new Vector3(0.41221f, 0.41221f, 0.41221f)
                    }
                }
            });

            // A more complex Kitbash piece, this has a prepared GameObject for Kitbash to build upon
            AssetBundle kitbashAssetBundle = AssetUtils.LoadAssetBundleFromResources("kitbash");
            try
            {
                KitbashObject kitbashObject = KitbashManager.Instance.AddKitbash(kitbashAssetBundle.LoadAsset<GameObject>("piece_odin_statue"), new KitbashConfig
                {
                    Layer = "piece",
                    KitbashSources = new List<KitbashSourceConfig>
                    {
                        new KitbashSourceConfig
                        {
                            SourcePrefab = "piece_artisanstation",
                            SourcePath = "ArtisanTable_Destruction/ArtisanTable_Destruction.007_ArtisanTable.019",
                            TargetParentPath = "new",
                            Position = new Vector3(-1.185f, -0.465f, 1.196f),
                            Rotation = Quaternion.Euler(-90f, 0, 0),
                            Scale = Vector3.one,Materials = new string[]{
                                "obsidian_nosnow",
                                "bronze"
                            }
                        },
                        new KitbashSourceConfig
                        {
                            SourcePrefab = "guard_stone",
                            SourcePath = "new/default",
                            TargetParentPath = "new/pivot",
                            Position = new Vector3(0, 0.0591f ,0),
                            Rotation = Quaternion.identity,
                            Scale = Vector3.one * 0.2f,
                            Materials = new string[]{
                                "bronze",
                                "obsidian_nosnow"
                            }
                        },
                    }
                });
                kitbashObject.OnKitbashApplied += () =>
                {
                    // We've added a CapsuleCollider to the skeleton, this is no longer needed
                    Object.Destroy(kitbashObject.Prefab.transform.Find("new/pivot/default").GetComponent<MeshCollider>());
                };
                PieceManager.Instance.AddPiece(
                    new CustomPiece(kitbashObject.Prefab, fixReference: false,
                        new PieceConfig
                        {
                            PieceTable = "Hammer",
                            Requirements = new RequirementConfig[]
                            {
                                new RequirementConfig { Item = "Obsidian" , Recover = true},
                                new RequirementConfig { Item = "Bronze", Recover = true }
                            }
                        }));
            }
            finally
            {
                kitbashAssetBundle.Unload(false);
            }
        }

        // Add custom pieces from an "empty" prefab with new piece categories
        private void AddPieceCategories()
        {
            CustomPiece CP = new CustomPiece("piece_lul", true, new PieceConfig
            {
                Name = "$piece_lul",
                Description = "$piece_lul_description",
                Icon = TestSprite,
                PieceTable = "Hammer",
                // ExtendStation = "piece_workbench", // Test station extension
                Category = "Lulzies."  // Test custom category
            });

            if (CP.PiecePrefab)
            {
                var prefab = CP.PiecePrefab;
                prefab.GetComponent<MeshRenderer>().material.mainTexture = TestTex;

                PieceManager.Instance.AddPiece(CP);
            }

            CP = new CustomPiece("piece_lel", true, new PieceConfig
            {
                Name = "$piece_lel",
                Description = "$piece_lel_description",
                Icon = TestSprite,
                PieceTable = "Hammer",
                ExtendStation = "piece_workbench", // Test station extension
                Category = "Lulzies."  // Test custom category
            });

            if (CP.PiecePrefab)
            {
                var prefab = CP.PiecePrefab;
                prefab.GetComponent<MeshRenderer>().material.mainTexture = TestTex;
                prefab.GetComponent<MeshRenderer>().material.color = Color.grey;

                PieceManager.Instance.AddPiece(CP);
            }
        }

        // Add items / pieces with errors on purpose to test error handling
        private void AddInvalidEntities()
        {
            CustomItem CI = new CustomItem("item_faulty", false);
            if (CI != null)
            {
                CI.ItemDrop.m_itemData.m_shared.m_icons = new Sprite[]
                {
                    TestSprite
                };
                ItemManager.Instance.AddItem(CI);

                CustomRecipe CR = new CustomRecipe(new RecipeConfig
                {
                    Item = "item_faulty",
                    Requirements = new RequirementConfig[]
                    {
                        new RequirementConfig { Item = "NotReallyThereResource", Amount = 99 }
                    }
                });
                ItemManager.Instance.AddRecipe(CR);
            }

            CustomPiece CP = new CustomPiece("piece_fukup", false, new PieceConfig
            {
                Icon = TestSprite,
                PieceTable = "Hammer",
                Requirements = new RequirementConfig[]
                {
                    new RequirementConfig { Item = "StillNotThereResource", Amount = 99 }
                }
            });

            if (CP != null)
            {
                PieceManager.Instance.AddPiece(CP);
            }
        }

        // Add new items as copies of vanilla items - just works when vanilla prefabs are already loaded (ObjectDB.CopyOtherDB for example)
        // You can use the Cache of the PrefabManager in here
        private void AddClonedItems()
        {
            try
            {
                // Create and add a custom item based on SwordBlackmetal
                ItemConfig evilSwordConfig = new ItemConfig();
                evilSwordConfig.Name = "$item_evilsword";
                evilSwordConfig.Description = "$item_evilsword_desc";
                evilSwordConfig.CraftingStation = "piece_workbench";
                evilSwordConfig.AddRequirement(new RequirementConfig("Stone", 1));
                evilSwordConfig.AddRequirement(new RequirementConfig("Wood", 1));

                CustomItem evilSword = new CustomItem("EvilSword", "SwordBlackmetal", evilSwordConfig);
                ItemManager.Instance.AddItem(evilSword);

                // Add our custom status effect to it
                var itemDrop = evilSword.ItemDrop;
                itemDrop.m_itemData.m_shared.m_equipStatusEffect = EvilSwordEffect.StatusEffect;

                // Create and add a recipe for the copied item
                RecipeConfig recipeConfig = new RecipeConfig();
                recipeConfig.Name = "Recipe_EvilSword2";
                recipeConfig.Item = "EvilSword";
                recipeConfig.CraftingStation = "piece_workbench";
                recipeConfig.AddRequirement(new RequirementConfig("Stone", 2));
                recipeConfig.AddRequirement(new RequirementConfig("Wood", 3));

                ItemManager.Instance.AddRecipe(new CustomRecipe(recipeConfig));

                // Create custom KeyHints for the item
                KeyHintConfig KHC = new KeyHintConfig
                {
                    Item = "EvilSword",
                    ButtonConfigs = new[]
                    {
                        // Override vanilla "Attack" key text
                        new ButtonConfig { Name = "Attack", HintToken = "$evilsword_shwing" },
                        // New custom input
                        EvilSwordSpecialButton,
                        // Override vanilla "Mouse Wheel" text
                        new ButtonConfig { Name = "Scroll", Axis = "Mouse ScrollWheel", HintToken = "$evilsword_scroll" }
                    }
                };
                KeyHintManager.Instance.AddKeyHint(KHC);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding cloned item: {ex}");
            }
            finally
            {
                // You want that to run only once, Jotunn has the item cached for the game session
                PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
            }
        }

        // Test the variant config for items
        private void AddVariants()
        {
            try
            {
                Sprite var1 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var1.png");
                Sprite var2 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var2.png");
                Texture2D styleTex = AssetUtils.LoadTexture("TestMod/Assets/test_varpaint.png");
                CustomItem CI = new CustomItem("item_lulvariants", "ShieldWood", new ItemConfig
                {
                    Name = "$lulz_shield",
                    Description = "$lulz_shield_desc",
                    Icons = new []
                    {
                        var1, var2
                    },
                    StyleTex = styleTex,
                    Requirements = new []
                    {
                        new RequirementConfig { Item = "Wood" }
                    }
                });
                ItemManager.Instance.AddItem(CI);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding variant item: {ex}");
            }
            finally
            {
                // You want that to run only once, Jotunn has the item cached for the game session
                PrefabManager.OnVanillaPrefabsAvailable -= AddVariants;
            }
        }

        private void AddCustomVariants()
        {
            try
            {
                Sprite var1 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var1.png");
                Sprite var2 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var2.png");
                Sprite var3 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var3.png");
                Sprite var4 = AssetUtils.LoadSpriteFromFile("TestMod/Assets/test_var4.png");
                Texture2D styleTex = AssetUtils.LoadTexture("TestMod/Assets/test_varpaint.png");

                ItemConfig shieldConfig = new ItemConfig();
                shieldConfig.Name = "$lulz_shield";
                shieldConfig.Description = "$lulz_shield_desc";
                shieldConfig.AddRequirement(new RequirementConfig("Wood", 1));
                shieldConfig.Icons = new Sprite[] { var1, var2, var3, var4 };
                shieldConfig.StyleTex = styleTex;
                ItemManager.Instance.AddItem(new CustomItem("item_lulzshield", "ShieldWood", shieldConfig));

                ItemConfig swordConfig = new ItemConfig();
                swordConfig.Name = "$lulz_sword";
                swordConfig.Description = "$lulz_sword_desc";
                swordConfig.AddRequirement(new RequirementConfig("Stone", 1));
                swordConfig.Icons = new Sprite[] { var1, var2, var3, var4 };
                swordConfig.StyleTex = styleTex;
                ItemManager.Instance.AddItem(new CustomItem("item_lulzsword", "SwordBronze", swordConfig));

                CustomItem cape = new CustomItem("item_lulzcape", "CapeLinen", new ItemConfig
                {
                    Name = "Lulz Cape",
                    Description = "Lulz to the rescue",
                    Icons = new []
                    {
                        var1, var2, var3, var4
                    },
                    StyleTex = styleTex,
                    Requirements = new []
                    {
                        new RequirementConfig { Item = "Wood" }
                    }
                });
                ItemManager.Instance.AddItem(cape);

                Texture2D styleTexArmor = AssetUtils.LoadTexture("TestMod/Assets/test_varpaintarmor.png");
                CustomItem chest = new CustomItem("item_lulzchest", "ArmorIronChest", new ItemConfig
                {
                    Name = "Lulz Armor",
                    Description = "Lol, that hit",
                    Icons = new []
                    {
                        var1, var2, var3, var4
                    },
                    StyleTex = styleTexArmor,
                    Requirements = new []
                    {
                        new RequirementConfig { Item = "Wood" }
                    }
                });
                ItemManager.Instance.AddItem(chest);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding variant item: {ex}");
            }
            finally
            {
                // You want that to run only once, Jotunn has the item cached for the game session
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomVariants;
            }
        }

        // Create rendered icons from prefabs
        private void AddItemsWithRenderedIcons()
        {
            // use the vanilla beech tree prefab to render our icon from
            GameObject beech = PrefabManager.Instance.GetPrefab("Beech1");

            // rendered the icon
            Sprite renderedIcon = RenderManager.Instance.Render(beech, RenderManager.IsometricRotation);

            // create the custom item with the icon
            ItemConfig treeItemConfig = new ItemConfig();
            treeItemConfig.Name = "$rendered_tree";
            treeItemConfig.Description = "$rendered_tree_desc";
            treeItemConfig.Icons = new Sprite[] { renderedIcon };
            treeItemConfig.AddRequirement(new RequirementConfig("Wood", 2, 0, true));

            ItemManager.Instance.AddItem(new CustomItem("item_MyTree", "BeechSeeds", treeItemConfig));

            // You want that to run only once, Jotunn has the item cached for the game session
            PrefabManager.OnVanillaPrefabsAvailable -= AddItemsWithRenderedIcons;
        }

        private void AddCustomLocationsAndVegetation()
        {
            AssetBundle locationsAssetBundle = AssetUtils.LoadAssetBundleFromResources("custom_locations");
            try
            {
                // Create location from AssetBundle using spawners and random spawns
                var spawnerLocation = locationsAssetBundle.LoadAsset<GameObject>("SpawnerLocation");

                var spawnerConfig = new LocationConfig();
                spawnerConfig.Biome = Heightmap.Biome.Meadows;
                spawnerConfig.Quantity = 100;
                spawnerConfig.Priotized = true;
                spawnerConfig.ExteriorRadius = 2f;
                spawnerConfig.MinAltitude = 1f;
                spawnerConfig.ClearArea = true;

                ZoneManager.Instance.AddCustomLocation(new CustomLocation(spawnerLocation, true, spawnerConfig));

                // Use empty location containers for locations instantiated in code
                var lulzCubePrefab = PrefabManager.Instance.GetPrefab("piece_lel");
                var cubesLocation = ZoneManager.Instance.CreateLocationContainer("lulzcube_location");

                // Stack of lulzcubes to easily spot the instances
                for (int i = 0; i < 10; i++)
                {
                    var lulzCube = Instantiate(lulzCubePrefab, cubesLocation.transform);
                    lulzCube.name = lulzCubePrefab.name;
                    lulzCube.transform.localPosition = new Vector3(0, i + 3, 0);
                    lulzCube.transform.localRotation = Quaternion.Euler(0, i * 30, 0);
                }

                var cubesConfig = new LocationConfig();
                cubesConfig.Biome = Heightmap.Biome.Meadows;
                cubesConfig.Quantity = 100;
                cubesConfig.Priotized = true;
                cubesConfig.ExteriorRadius = 2f;
                cubesConfig.ClearArea = true;

                ZoneManager.Instance.AddCustomLocation(new CustomLocation(cubesLocation, false, cubesConfig));

                // Use vegetation for singular prefabs
                var singleLulz = new VegetationConfig();
                singleLulz.Biome = Heightmap.Biome.Meadows;
                singleLulz.BlockCheck = true;

                ZoneManager.Instance.AddCustomVegetation(new CustomVegetation(lulzCubePrefab, false, singleLulz));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while adding custom locations: {ex}");
            }
            finally
            {
                // Custom locations and vegetations are added every time the game loads, we don't need to add every time
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomLocationsAndVegetation;
                locationsAssetBundle.Unload(false);
            }
        }

        private void AddClonedVanillaLocationsAndVegetations()
        {
            try
            {
                var lulzCubePrefab = PrefabManager.Instance.GetPrefab("piece_lal");

                // Create a clone of a vanilla location
                CustomLocation myEikthyrLocation =
                    ZoneManager.Instance.CreateClonedLocation("MyEikthyrAltar", "Eikthyrnir");
                myEikthyrLocation.ZoneLocation.m_exteriorRadius = 1f; // Easy to place :D
                myEikthyrLocation.ZoneLocation.m_quantity = 20; // MOAR

                // Stack of lulzcubes to easily spot the instances
                for (int i = 0; i < 40; i++)
                {
                    var lulzCube = Instantiate(lulzCubePrefab, myEikthyrLocation.ZoneLocation.m_prefab.transform);
                    lulzCube.name = lulzCubePrefab.name;
                    lulzCube.transform.localPosition = new Vector3(0, i + 3, 0);
                    lulzCube.transform.localRotation = Quaternion.Euler(0, i * 30, 0);
                }

                // Add more seed carrots to the meadows & black forest
                ZoneSystem.ZoneVegetation pickableSeedCarrot = ZoneManager.Instance.GetZoneVegetation("Pickable_SeedCarrot");

                var carrotSeed = new VegetationConfig(pickableSeedCarrot);
                carrotSeed.Min = 3;
                carrotSeed.Max = 10;
                carrotSeed.GroupSizeMin = 3;
                carrotSeed.GroupSizeMax = 10;
                carrotSeed.GroupRadius = 10;
                carrotSeed.Biome = ZoneManager.AnyBiomeOf(Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest);

                ZoneManager.Instance.AddCustomVegetation(new CustomVegetation(pickableSeedCarrot.m_prefab, false, carrotSeed));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while adding cloned locations: {ex}");
            }
            finally
            {
                // Custom locations and vegetations are added every time the game loads, we don't need to add every time
                ZoneManager.OnVanillaLocationsAvailable -= AddClonedVanillaLocationsAndVegetations;
            }

        }

        private void ModifyVanillaLocationsAndVegetation()
        {
            var lulzCubePrefab = PrefabManager.Instance.GetPrefab("piece_lul");

            // Modify existing locations
            var eikhtyrLocation = ZoneManager.Instance.GetZoneLocation("Eikthyrnir");
            eikhtyrLocation.m_exteriorRadius = 20f; //More space around the altar

            var eikhtyrCube = Instantiate(lulzCubePrefab, eikhtyrLocation.m_prefab.transform);
            eikhtyrCube.name = lulzCubePrefab.name;
            eikhtyrCube.transform.localPosition = new Vector3(-8.52f, 5.37f, -0.92f);

            // Modify existing vegetation
            var raspberryBush = ZoneManager.Instance.GetZoneVegetation("RaspberryBush");
            raspberryBush.m_groupSizeMin = 10;
            raspberryBush.m_groupSizeMax = 30;

            // Add a prefab with a ZNetView component
            var woodHouse3 = ZoneManager.Instance.GetZoneLocation("WoodHouse3");
            var thistlePrefab = PrefabManager.Instance.GetPrefab("Pickable_Thistle");
            var thistle = Instantiate(thistlePrefab, woodHouse3.m_prefab.transform);
            thistle.transform.localPosition = new Vector3(-4.48f, 0f, 3f);

            // Not unregistering this hook, it needs to run every world load
        }
        
        // Add custom made creatures using world spawns and drop lists
        private void AddCustomCreaturesAndSpawns()
        {
            AssetBundle creaturesAssetBundle = AssetUtils.LoadAssetBundleFromResources("creatures");
            try
            {
                // Load LulzCube test texture and sprite
                var lulztex = AssetUtils.LoadTexture("TestMod/Assets/test_tex.jpg");
                var lulzsprite = Sprite.Create(lulztex, new Rect(0f, 0f, lulztex.width, lulztex.height), Vector2.zero);

                // Create an optional drop/consume item for this creature
                CreateDropConsumeItem(lulzsprite, lulztex);

                // Load and create a custom animal creature
                CreateAnimalCreature(creaturesAssetBundle, lulztex);
                
                // Load and create a custom monster creature
                CreateMonsterCreature(creaturesAssetBundle, lulztex);
                
                // Add localization for all stuff added
                Localization.AddTranslation("English", new Dictionary<string, string>
                {
                    {"item_lulzanimalparts", "Parts of a Lulz Animal"},
                    {"item_lulzanimalparts_desc", "Remains of a LulzAnimal. It still giggles when touched."},
                    {"creature_lulzanimal", "Lulz Animal"},
                    {"creature_lulzmonster", "Lulz Monster"}
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while adding custom creatures: {ex}");
            }
            finally
            {
                creaturesAssetBundle.Unload(false);
            }
        }

        private void CreateDropConsumeItem(Sprite lulzsprite, Texture2D lulztex)
        {
            // Create a little lulz cube as the drop and consume item for both creatures
            ItemConfig lulzCubeConfig = new ItemConfig();
            lulzCubeConfig.Name = "$item_lulzanimalparts";
            lulzCubeConfig.Description = "$item_lulzanimalparts_desc";
            lulzCubeConfig.Icons = new[] {lulzsprite};

            var lulzItem = new CustomItem("item_lul", true, lulzCubeConfig);
            lulzItem.ItemDrop.m_itemData.m_shared.m_maxStackSize = 20;
            lulzItem.ItemPrefab.AddComponent<Rigidbody>();

            // Set our lulzcube test texture on the first material found
            lulzItem.ItemPrefab.GetComponentInChildren<MeshRenderer>().material.mainTexture = lulztex;

            // Make it smol
            lulzItem.ItemPrefab.GetComponent<ZNetView>().m_syncInitialScale = true;
            lulzItem.ItemPrefab.transform.localScale = new Vector3(0.33f, 0.33f, 0.33f);

            // Add to the ItemManager
            ItemManager.Instance.AddItem(lulzItem);
        }
        
        private void CreateAnimalCreature(AssetBundle creaturesAssetBundle, Texture2D lulztex)
        {
            // Load creature prefab from AssetBundle
            var lulzAnimalPrefab = creaturesAssetBundle.LoadAsset<GameObject>("LulzAnimal");

            // Set our lulzcube test texture on the first material found
            lulzAnimalPrefab.GetComponentInChildren<MeshRenderer>().material.mainTexture = lulztex;

            // Create a custom creature using our drop item and spawn configs
            var lulzAnimalConfig = new CreatureConfig();
            lulzAnimalConfig.Name = "$creature_lulzanimal";
            lulzAnimalConfig.Faction = Character.Faction.AnimalsVeg;
            lulzAnimalConfig.AddDropConfig(new DropConfig
            {
                Item = "item_lul",
                Chance = 100f,
                LevelMultiplier = false,
                MinAmount = 1,
                MaxAmount = 3,
                //OnePerPlayer = true
            });
            lulzAnimalConfig.AddSpawnConfig(new SpawnConfig
            {
                Name = "Jotunn_LulzAnimalSpawn1",
                SpawnChance = 100f,
                SpawnInterval = 1f,
                SpawnDistance = 1f,
                MaxSpawned = 10,
                Biome = Heightmap.Biome.Meadows
            });
            lulzAnimalConfig.AddSpawnConfig(new SpawnConfig
            {
                Name = "Jotunn_LulzAnimalSpawn2",
                SpawnChance = 50f,
                SpawnInterval = 2f,
                SpawnDistance = 2f,
                MaxSpawned = 5,
                Biome = ZoneManager.AnyBiomeOf(Heightmap.Biome.BlackForest, Heightmap.Biome.Plains)
            });

            // Add it to the manager
            CreatureManager.Instance.AddCreature(new CustomCreature(lulzAnimalPrefab, false, lulzAnimalConfig));
        }
        
        private void CreateMonsterCreature(AssetBundle creaturesAssetBundle, Texture2D lulztex)
        {
            // Load creature prefab from AssetBundle
            var lulzMonsterPrefab = creaturesAssetBundle.LoadAsset<GameObject>("LulzMonster");

            // Set our lulzcube test texture on the first material found
            lulzMonsterPrefab.GetComponentInChildren<MeshRenderer>().material.mainTexture = lulztex;

            // Create a custom creature using our consume item and spawn configs
            var lulzMonsterConfig = new CreatureConfig();
            lulzMonsterConfig.Name = "$creature_lulzmonster";
            lulzMonsterConfig.Faction = Character.Faction.ForestMonsters;
            lulzMonsterConfig.UseCumulativeLevelEffects = true;
            lulzMonsterConfig.AddConsumable("item_lul");
            lulzMonsterConfig.AddSpawnConfig(new SpawnConfig
            {
                Name = "Jotunn_LulzMonsterSpawn1",
                SpawnChance = 100f,
                MaxSpawned = 1,
                Biome = Heightmap.Biome.Meadows
            });
            lulzMonsterConfig.AddSpawnConfig(new SpawnConfig
            {
                Name = "Jotunn_LulzMonsterSpawn2",
                SpawnChance = 50f,
                MaxSpawned = 1,
                Biome = ZoneManager.AnyBiomeOf(Heightmap.Biome.BlackForest, Heightmap.Biome.Plains)
            });

            // Add it to the manager
            CreatureManager.Instance.AddCreature(new CustomCreature(lulzMonsterPrefab, true, lulzMonsterConfig));
        }

        // Modify and clone vanilla creatures
        private void ModifyAndCloneVanillaCreatures()
        {
            // Clone a vanilla creature with and add new spawn information
            var lulzetonConfig = new CreatureConfig();
            lulzetonConfig.AddSpawnConfig(new SpawnConfig
            {
                Name = "Jotunn_SkelSpawn1",
                SpawnChance = 100,
                SpawnInterval = 20f,
                SpawnDistance = 1f,
                Biome = Heightmap.Biome.Meadows,
                MinLevel = 3
            });

            var lulzeton = new CustomCreature("Lulzeton", "Skeleton_NoArcher", lulzetonConfig);
            var lulzoid = lulzeton.Prefab.GetComponent<Humanoid>();
            lulzoid.m_walkSpeed = 0.1f;
            CreatureManager.Instance.AddCreature(lulzeton);

            // Get a vanilla creature prefab and change some values
            var skeleton = CreatureManager.Instance.GetCreaturePrefab("Skeleton_NoArcher");
            var humanoid = skeleton.GetComponent<Humanoid>();
            humanoid.m_walkSpeed = 2;

            // Unregister the hook, modified and cloned creatures are kept over the whole game session
            CreatureManager.OnVanillaCreaturesAvailable -= ModifyAndCloneVanillaCreatures;
        }

        // Set version of the plugin for the mod compatibility test
        private void SetVersion()
        {
            var propinfo = Info.Metadata.GetType().GetProperty("Version", BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);

            // Change version number of this module if test is enabled
            if (EnableVersionMismatch.Value)
            {
                var v = new System.Version(0, 0, 0);
                propinfo.SetValue(Info.Metadata, v, null);
            }
            else
            {
                propinfo.SetValue(Info.Metadata, CurrentVersion, null);
            }
        }

        [HarmonyPatch(typeof(Version), nameof(Version.GetVersionString)), HarmonyPostfix]
        private static void Version_GetVersionString(ref string __result)
        {
            __result = EnableExtVersionMismatch.Value ? "Non.Business.You" : __result;
        }
    }
}
