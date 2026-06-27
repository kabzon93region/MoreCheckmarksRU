using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static EFT.HalloweenEventVisual;

namespace MoreCheckmarks
{
    public struct NeededStruct
    {
        public bool foundNeeded;
        public bool foundFulfilled;
        public int possessedCount;
        public int requiredCount;
    }

    [BepInPlugin("com.dematch.morecheckmarks.ru", "MoreCheckmarks RU", "2.1.0.7")]
    public class MoreCheckmarksMod : BaseUnityPlugin
    {
        // BepinEx
        public const string pluginGuid = "com.dematch.morecheckmarks.ru";
        public const string pluginName = "MoreCheckmarks RU";
        public const string pluginVersion = "2.1.0.7";

        // Config Entries (BepInEx F12 menu)
        public static ConfigEntry<bool> configFulfilledAnyCanBeUpgraded;
        public static ConfigEntry<int> configQuestPriority;
        public static ConfigEntry<int> configHideoutPriority;
        public static ConfigEntry<int> configWishlistPriority;
        public static ConfigEntry<int> configBarterPriority;
        public static ConfigEntry<int> configCraftPriority;
        public static ConfigEntry<bool> configShowFutureModulesLevels;
        public static ConfigEntry<bool> configShowBarter;
        public static ConfigEntry<bool> configShowCraft;
        public static ConfigEntry<bool> configShowFutureCraft;
        public static ConfigEntry<Color> configNeedMoreColor;
        public static ConfigEntry<Color> configFulfilledColor;
        public static ConfigEntry<Color> configWishlistColor;
        public static ConfigEntry<Color> configBarterColor;
        public static ConfigEntry<Color> configCraftColor;
        public static ConfigEntry<bool> configIncludeFutureQuests;
        public static ConfigEntry<bool> configShowPrerequisiteQuests;
        public static ConfigEntry<bool> configShowQuestCheckmarksNonFIR;
        public static ConfigEntry<bool> configDebugHideoutTooltipLogs;
        public static ConfigEntry<bool> configDebugLogEveryTooltip;

        // Config settings (derived from ConfigEntry values)
        public static bool fulfilledAnyCanBeUpgraded => configFulfilledAnyCanBeUpgraded.Value;
        public static int questPriority => configQuestPriority.Value;
        public static int hideoutPriority => configHideoutPriority.Value;
        public static int wishlistPriority => configWishlistPriority.Value;
        public static int barterPriority => configBarterPriority.Value;
        public static int craftPriority => configCraftPriority.Value;
        public static bool showFutureModulesLevels => configShowFutureModulesLevels.Value;
        public static bool showBarter => configShowBarter.Value;
        public static bool showCraft => configShowCraft.Value;
        public static bool showFutureCraft => configShowFutureCraft.Value;
        public static bool includeFutureQuests => configIncludeFutureQuests.Value;
        public static bool showPrerequisiteQuests => configShowPrerequisiteQuests.Value;
        public static bool showQuestCheckmarksNonFIR => configShowQuestCheckmarksNonFIR.Value;
        public static bool debugHideoutTooltipLogs => configDebugHideoutTooltipLogs.Value;
        public static bool debugLogEveryTooltip => configDebugLogEveryTooltip.Value;

        // Parsed colors (updated when config changes)
        public static Color needMoreColor = new Color(1, 0.37255f, 0.37255f);
        public static Color fulfilledColor = new Color(0.30588f, 1, 0.27843f);
        public static Color wishlistColor = new Color(0, 0, 1);
        public static Color barterColor = new Color(1, 0, 1);
        public static Color craftColor = new Color(0, 1, 1);

        // Assets
        public static Sprite whiteCheckmark;
        private static TMP_FontAsset benderBold;
        public static string modPath;

        // Live
        public static MoreCheckmarksMod modInstance;

        // Quest IDs and Names by items in their requirements
        public static Dictionary<string, QuestPair>
            questDataStartByItemTemplateID = new Dictionary<string, QuestPair>();

        public static Dictionary<string, Dictionary<string, int>> neededStartItemsByQuest =
            new Dictionary<string, Dictionary<string, int>>();

        public static Dictionary<string, QuestPair> questDataCompleteByItemTemplateID =
            new Dictionary<string, QuestPair>();

        public static Dictionary<string, Dictionary<string, int>> neededCompleteItemsByQuest =
            new Dictionary<string, Dictionary<string, int>>();

        public class QuestPair
        {
            // Key: quest name key, Value: (QuestName, QuestId)
            public Dictionary<string, (string questName, string questId)> questData = new Dictionary<string, (string, string)>();
            public int count;
        }

        // Quest prerequisite tracking
        public static Dictionary<string, HashSet<string>> questPrerequisites = new Dictionary<string, HashSet<string>>();
        public static Dictionary<string, HashSet<string>> prereqCache = new Dictionary<string, HashSet<string>>();
        public static HashSet<string> completedQuestIds = new HashSet<string>();
        public static HashSet<string> debugLoggedItemTemplates = new HashSet<string>();

        // Flag to track if quest data needs to be reloaded (e.g., profile data wasn't available on first load)
        public static bool questDataNeedsReload = false;

        public static JObject itemData;
        public static JObject locales;

        public static Dictionary<string, string> productionEndProductByID = new Dictionary<string, string>();

        // Barter item name and amount of price by items in price
        public static List<Dictionary<string, List<KeyValuePair<string, int>>>> bartersByItemByTrader =
            new List<Dictionary<string, List<KeyValuePair<string, int>>>>();

        public static string[] traders =
        {
            "Prapor", "Therapist", "Fence", "Skier", "Peacekeeper", "Mechanic", "Ragman", "Jaeger", "Lighthouse keeper"
        };

        public static int[] priorities = { 0, 1, 2, 3, 4 };
        public static bool[] neededFor = new bool[5];

        public static Color[] colors = { Color.yellow, needMoreColor, wishlistColor, barterColor, craftColor };

        private void Awake()
        {
            Logger.LogInfo("MoreCheckmarks RU Awake (fork TommySoucy/MoreCheckmarks 2.1.0, v" + pluginVersion + ")");

            modInstance = this;

            Init();
        }

        private void Init()
        {
            modPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(MoreCheckmarksMod)).Location);
            if (modPath == null)
            {
                Logger.LogError("MoreCheckmarks Mod Path is null");
                return;
            }

            modPath = modPath.Replace('\\', '/');

            BindConfig();

            LoadAssets();

            LoadData();

            DoPatching();
        }

        public void LoadData()
        {
            LogInfo("Loading data");
            LogInfo("\tQuests");

            // Clear all quest data first - this ensures we start fresh even if loading fails
            questDataStartByItemTemplateID.Clear();
            neededStartItemsByQuest.Clear();
            questDataCompleteByItemTemplateID.Clear();
            neededCompleteItemsByQuest.Clear();
            questPrerequisites.Clear();
            prereqCache.Clear();
            completedQuestIds.Clear();

            JArray questData;
            try
            {
                var questResponse = RequestHandler.GetJson("/MoreCheckmarksRoutes/quests");
                if (string.IsNullOrEmpty(questResponse) || questResponse == "null")
                {
                    LogInfo("Quest data response was empty or null (new profile?). Quest checkmarks will be unavailable until data is loaded.");
                    questData = new JArray();
                }
                else
                {
                    questData = JArray.Parse(questResponse);
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse quest data: {ex.Message}. Quest checkmarks will be unavailable.");
                questData = new JArray();
            }

            LogInfo($"Loaded {questData.Count} quests");

            // If quest data is empty, flag for reload on next item view (handles new profile case)
            // Track if quest data is empty (profile may not be fully ready yet)
            questDataNeedsReload = questData.Count == 0;
            if (questDataNeedsReload)
            {
                LogInfo("Quest data empty - will reload when a quest is accepted");
            }

            foreach (var t in questData)
            {
                if (t["conditions"] != null && t["conditions"]["AvailableForFinish"] != null)
                {
                    var availableForFinishConditions = t["conditions"]["AvailableForFinish"] as JArray;
                    for (int j = 0; j != availableForFinishConditions.Count; ++j)
                    {
                        if (availableForFinishConditions[j]["conditionType"] != null)
                        {
                            if (availableForFinishConditions[j]["conditionType"].ToString().Equals("HandoverItem"))
                            {
                                if (availableForFinishConditions[j]["target"] != null)
                                {
                                    var targets = availableForFinishConditions[j]["target"] as JArray;
                                    for (int k = 0; k != targets.Count; ++k)
                                    {
                                        if (questDataCompleteByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataCompleteByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededCompleteItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededCompleteItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " finish condition " + j +
                                             " of type HandoverItem missing target");
                                }
                            }

                            if (availableForFinishConditions[j]["conditionType"].ToString().Equals("FindItem"))
                            {
                                if (availableForFinishConditions[j]["target"] != null)
                                {
                                    var targets = availableForFinishConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        // Check if there is a hand in item condition for the same item and at least the same count
                                        // If so skip this, we will count the hand in instead
                                        var foundInHandin = false;
                                        for (var l = 0; l < availableForFinishConditions.Count; ++l)
                                        {
                                            if (availableForFinishConditions[l]["conditionType"].ToString()
                                                .Equals("HandoverItem"))
                                            {
                                                if (availableForFinishConditions[l]["target"] is JArray handInTargets &&
                                                    StringJArrayContainsString(handInTargets, targets[k].ToString()) &&
                                                    (!int.TryParse(availableForFinishConditions[l]["value"].ToString(),
                                                         out var parsedValue) ||
                                                     !int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                         out var currentParsedValue) ||
                                                     parsedValue == currentParsedValue))
                                                {
                                                    foundInHandin = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (foundInHandin)
                                        {
                                            continue;
                                        }

                                        if (questDataCompleteByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataCompleteByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededCompleteItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededCompleteItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " finish condition " + j +
                                             " of type FindItem missing target");
                                }
                            }

                            if (availableForFinishConditions[j]["conditionType"].ToString()
                                .Equals("LeaveItemAtLocation"))
                            {
                                if (availableForFinishConditions[j]["target"] != null)
                                {
                                    var targets = availableForFinishConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        if (questDataCompleteByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataCompleteByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededCompleteItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededCompleteItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " finish condition " + j +
                                             " of type LeaveItemAtLocation missing target");
                                }
                            }

                            if (availableForFinishConditions[j]["conditionType"].ToString().Equals("PlaceBeacon"))
                            {
                                if (availableForFinishConditions[j]["target"] != null)
                                {
                                    var targets = availableForFinishConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        if (questDataCompleteByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataCompleteByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededCompleteItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForFinishConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededCompleteItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " finish condition " + j +
                                             " of type PlaceBeacon missing target");
                                }
                            }
                        }
                        else
                        {
                            LogError("Quest " + t["_id"].ToString() + " finish condition " + j +
                                     " missing condition type");
                        }
                    }
                }
                else
                {
                    LogError("Quest " + t["_id"].ToString() + " missing finish conditions");
                }

                if (t["conditions"] != null && t["conditions"]["AvailableForFinish"] != null)
                {
                    var availableForStartConditions = t["conditions"]["AvailableForStart"] as JArray;
                    for (var j = 0; j < availableForStartConditions.Count; ++j)
                    {
                        if (availableForStartConditions[j]["conditionType"] != null)
                        {
                            if (availableForStartConditions[j]["conditionType"].ToString().Equals("HandoverItem"))
                            {
                                if (availableForStartConditions[j]["target"] != null)
                                {
                                    var targets = availableForStartConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        if (questDataStartByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataStartByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededStartItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededStartItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " start condition " + j +
                                             " of type HandoverItem missing target");
                                }
                            }

                            if (availableForStartConditions[j]["conditionType"].ToString().Equals("FindItem"))
                            {
                                if (availableForStartConditions[j]["target"] != null)
                                {
                                    var targets = availableForStartConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        // Check if there is a hand in item condition for the same item and at least the same count
                                        // If so skip this, we will count the hand in instead
                                        var foundInHandin = false;
                                        for (var l = 0; l < availableForStartConditions.Count; ++l)
                                        {
                                            if (availableForStartConditions[l]["conditionType"].ToString().Equals("HandoverItem"))
                                            {
                                                if (availableForStartConditions[l]["target"] is JArray handInTargets &&
                                                    StringJArrayContainsString(handInTargets, targets[k].ToString()) &&
                                                    (!int.TryParse(availableForStartConditions[l]["value"].ToString(),
                                                         out var parsedValue) ||
                                                     !int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                         out var currentParsedValue) ||
                                                     parsedValue == currentParsedValue))
                                                {
                                                    foundInHandin = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (foundInHandin)
                                        {
                                            continue;
                                        }

                                        if (questDataStartByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataStartByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededStartItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededStartItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " start condition " + j +
                                             " of type FindItem missing target");
                                }
                            }

                            if (availableForStartConditions[j]["conditionType"].ToString()
                                .Equals("LeaveItemAtLocation"))
                            {
                                if (availableForStartConditions[j]["target"] != null)
                                {
                                    var targets = availableForStartConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        if (questDataStartByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataStartByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededStartItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededStartItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " start condition " + j +
                                             " of type LeaveItemAtLocation missing target");
                                }
                            }

                            if (availableForStartConditions[j]["conditionType"].ToString().Equals("PlaceBeacon"))
                            {
                                if (availableForStartConditions[j]["target"] != null)
                                {
                                    var targets = availableForStartConditions[j]["target"] as JArray;
                                    for (var k = 0; k < targets.Count; ++k)
                                    {
                                        if (questDataStartByItemTemplateID.TryGetValue(targets[k].ToString(),
                                                out var quests))
                                        {
                                            if (!quests.questData.ContainsKey(t["name"].ToString()))
                                            {
                                                quests.questData.Add(t["name"].ToString(),
                                                    (t["QuestName"].ToString(), t["_id"].ToString()));
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            quests.count += parsedValue;
                                        }
                                        else
                                        {
                                            var newPair = new QuestPair();
                                            newPair.questData.Add(t["name"].ToString(),
                                                (t["QuestName"].ToString(), t["_id"].ToString()));
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newPair.count = parsedValue;
                                            questDataStartByItemTemplateID.Add(targets[k].ToString(), newPair);
                                        }

                                        if (neededStartItemsByQuest.TryGetValue(t["_id"].ToString(),
                                                out var items))
                                        {
                                            if (!items.ContainsKey(targets[k].ToString()))
                                            {
                                                items.Add(targets[k].ToString(), 0);
                                            }

                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            items[targets[k].ToString()] += parsedValue;
                                        }
                                        else
                                        {
                                            var newDict = new Dictionary<string, int>();
                                            int.TryParse(availableForStartConditions[j]["value"].ToString(),
                                                out var parsedValue);
                                            newDict.Add(targets[k].ToString(), parsedValue);
                                            neededStartItemsByQuest.Add(t["_id"].ToString(), newDict);
                                        }
                                    }
                                }
                                else
                                {
                                    LogError("Quest " + t["_id"].ToString() + " start condition " + j +
                                             " of type PlaceBeacon missing target");
                                }
                            }
                        }
                        else
                        {
                            LogError("Quest " + t["_id"].ToString() + " start condition " + j +
                                     " missing condition type");
                        }
                    }
                }
                else
                {
                    LogError("Quest " + t["_id"].ToString() + " missing start conditions");
                }
            }

            // Build prerequisite map from quest conditions
            LogInfo("\tBuilding prerequisite map");
            foreach (var quest in questData)
            {
                var questId = quest["_id"]?.ToString();
                if (string.IsNullOrEmpty(questId)) continue;

                var prereqs = new HashSet<string>();
                var startConditions = quest["conditions"]?["AvailableForStart"] as JArray;
                if (startConditions != null)
                {
                    foreach (var condition in startConditions)
                    {
                        if (condition["conditionType"]?.ToString() == "Quest")
                        {
                            var targetQuestId = condition["target"]?.ToString();
                            if (!string.IsNullOrEmpty(targetQuestId))
                            {
                                prereqs.Add(targetQuestId);
                            }
                        }
                    }
                }
                questPrerequisites[questId] = prereqs;
            }
            LogInfo($"\tBuilt prerequisite map for {questPrerequisites.Count} quests");

            LogInfo("\tItems");
            var euro = "569668774bdc2da2298b4568";
            var rouble = "5449016a4bdc2d6f028b456f";
            var dollar = "5696686a4bdc2da3298b456a";
            if (itemData == null)
            {
                itemData = JObject.Parse(RequestHandler.GetJson("/MoreCheckmarksRoutes/items"));
            }

            LogInfo("\tAssorts");
            var assortData = JArray.Parse(RequestHandler.GetJson("/MoreCheckmarksRoutes/assorts"));
            bartersByItemByTrader.Clear();
            for (var i = 0; i < assortData.Count; ++i)
            {
                bartersByItemByTrader.Add(new Dictionary<string, List<KeyValuePair<string, int>>>());
                var items = assortData[i]["items"] as JArray;
                for (var j = 0; j < items.Count; ++j)
                {
                    if (items[j]["parentId"] != null && items[j]["parentId"].ToString().Equals("hideout"))
                    {
                        var barters = assortData[i]["barter_scheme"][items[j]["_id"].ToString()] as JArray;
                        for (var k = 0; k < barters.Count; ++k)
                        {
                            var barter = barters[k] as JArray;
                            for (var l = 0; l < barter.Count; ++l)
                            {
                                var priceTPL = barter[l]["_tpl"].ToString();
                                if (!priceTPL.Equals(euro) && !priceTPL.Equals(rouble) && !priceTPL.Equals(dollar))
                                {
                                    if (bartersByItemByTrader[i].TryGetValue(priceTPL,
                                            out var barterList))
                                    {
                                        barterList.Add(new KeyValuePair<string, int>(items[j]["_tpl"].ToString(),
                                            (int)(barter[l]["count"])));
                                    }
                                    else
                                    {
                                        bartersByItemByTrader[i].Add(priceTPL,
                                            new List<KeyValuePair<string, int>>()
                                            {
                                                new KeyValuePair<string, int>(items[j]["_tpl"].ToString(),
                                                    (int)(barter[l]["count"]))
                                            });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            LogInfo("\tProductions");
            var productionData = JObject.Parse(RequestHandler.GetJson("/MoreCheckmarksRoutes/productions"));
            productionEndProductByID.Clear();
            var productionRecipes = productionData["recipes"] as JArray;
            for (var i = 0; i < productionRecipes.Count; ++i)
            {
                productionEndProductByID.Add(productionRecipes[i]["_id"].ToString(),
                    productionRecipes[i]["endProduct"].ToString());
            }
        }

        private bool StringJArrayContainsString(JArray arr, string s)
        {
            for (var i = 0; i < arr.Count; ++i)
            {
                if (arr[i].ToString().Equals(s))
                {
                    return true;
                }
            }

            return false;
        }

        private void BindConfig()
        {
            // Note about changes requiring menu refresh
            Config.Bind(
                "0. Важное примечание",
                "Требуется обновление",
                "Переключите меню для применения",
                new ConfigDescription("Изменения применяются не сразу. Чтобы увидеть обновления: покиньте текущее меню (например, схрон), зайдите в главное меню и вернитесь обратно.", null, new ConfigurationManagerAttributes { ReadOnly = true, HideDefaultButton = true }));

            // Hideout Settings
            configFulfilledAnyCanBeUpgraded = Config.Bind(
                "Убежище",
                "Любой доступен для улучшения",
                true,
                "Если ВКЛ — показывает зелёную метку, когда ХОТЯ БЫ ОДИН модуль убежища можно улучшить. Если ВЫКЛ — только когда ВСЕ модули можно улучшить.");

            configShowFutureModulesLevels = Config.Bind(
                "Убежище",
                "Показывать будущие уровни",
                true,
                "ВКЛ — все будущие уровни модулей (в т.ч. ещё недоступные). ВЫКЛ — только зоны, где стройка/улучшение доступны сейчас, и только следующий уровень.");

            // Quest Settings
            configIncludeFutureQuests = Config.Bind(
                "Квесты",
                "Включать будущие квесты",
                true,
                "Учитывать будущие квесты при проверке, для каких квестов нужен предмет. Если выключено — работает как оригинал.");

            configShowPrerequisiteQuests = Config.Bind(
                "Квесты",
                "Показывать предв. квесты",
                true,
                "Показывать количество предварительных квестов, необходимых для открытия каждого квеста. Сортировка по количеству предв. квестов с цветовой маркировкой: зелёный (0), жёлтый (1-9), серый (10+).");

            configShowQuestCheckmarksNonFIR = Config.Bind(
                "Квесты",
                "Метки для ненайденных в рейде",
                false,
                "При включении метки квестов будут отображаться на предметах, даже если они не найдены в рейде. Полезно, если SPT настроен принимать ненайденные предметы для сдачи квестов.");

            // Debug Settings
            configDebugHideoutTooltipLogs = Config.Bind(
                "Отладка",
                "Логи hideout-подсказок",
                true,
                "Подробные логи расчета подсказок убежища (GetNeeded/tooltip). Для диагностики проблем с отображением.");

            configDebugLogEveryTooltip = Config.Bind(
                "Отладка",
                "Логировать каждый тултип",
                false,
                "Если ВЫКЛ — логируется только первое появление каждого itemTemplateId. Если ВКЛ — каждый вызов тултипа.");

            // Barter & Craft Settings
            configShowBarter = Config.Bind(
                "Бартер и крафт",
                "Показывать бартер",
                true,
                "Показывать метку и подсказку для бартеров/обменов, для которых нужен этот предмет.");

            configShowCraft = Config.Bind(
                "Бартер и крафт",
                "Показывать крафт",
                true,
                "Показывать метку и подсказку для крафтов, для которых нужен этот предмет.");

            configShowFutureCraft = Config.Bind(
                "Бартер и крафт",
                "Показывать будущий крафт",
                true,
                "Показывать крафты, которые ещё не открыты.");

            // Priority Settings (higher = takes precedence, ordered by default priority)
            configQuestPriority = Config.Bind(
                "Приоритет",
                "Приоритет квестов",
                4,
                new ConfigDescription("Приоритет меток квестов. Больше число = выше приоритет, если предмет нужен для нескольких целей.",
                    new AcceptableValueRange<int>(0, 10),
                    new ConfigurationManagerAttributes { Order = 5 }));

            configHideoutPriority = Config.Bind(
                "Приоритет",
                "Приоритет убежища",
                3,
                new ConfigDescription("Приоритет меток убежища. Больше число = выше приоритет.",
                    new AcceptableValueRange<int>(0, 10),
                    new ConfigurationManagerAttributes { Order = 4 }));

            configWishlistPriority = Config.Bind(
                "Приоритет",
                "Приоритет желаемого",
                2,
                new ConfigDescription("Приоритет меток списка желаемого. Больше число = выше приоритет.",
                    new AcceptableValueRange<int>(0, 10),
                    new ConfigurationManagerAttributes { Order = 3 }));

            configBarterPriority = Config.Bind(
                "Приоритет",
                "Приоритет бартера",
                1,
                new ConfigDescription("Приоритет меток бартера. Больше число = выше приоритет.",
                    new AcceptableValueRange<int>(0, 10),
                    new ConfigurationManagerAttributes { Order = 2 }));

            configCraftPriority = Config.Bind(
                "Приоритет",
                "Приоритет крафта",
                0,
                new ConfigDescription("Приоритет меток крафта. Больше число = выше приоритет.",
                    new AcceptableValueRange<int>(0, 10),
                    new ConfigurationManagerAttributes { Order = 1 }));

            // Color Settings (RGB sliders in F12 menu)
            configNeedMoreColor = Config.Bind(
                "Цвета",
                "Цвет «Нужно ещё»",
                new Color(1f, 0.37255f, 0.37255f),
                "Цвет для предметов, которых нужно ещё (по умолч.: светло-красный)");

            configFulfilledColor = Config.Bind(
                "Цвета",
                "Цвет «Выполнено»",
                new Color(0.30588f, 1f, 0.27843f),
                "Цвет для предметов, потребность в которых закрыта (по умолч.: светло-зелёный)");

            configWishlistColor = Config.Bind(
                "Цвета",
                "Цвет желаемого",
                new Color(0.23137f, 0.93725f, 1f),
                "Цвет для предметов из списка желаемого (по умолч.: голубой)");

            configBarterColor = Config.Bind(
                "Цвета",
                "Цвет бартера",
                new Color(1f, 0f, 1f),
                "Цвет для предметов бартера (по умолч.: пурпурный)");

            configCraftColor = Config.Bind(
                "Цвета",
                "Цвет крафта",
                new Color(0f, 1f, 1f),
                "Цвет для предметов крафта (по умолч.: голубой)");

            // Subscribe to config changes
            configNeedMoreColor.SettingChanged += (s, e) => UpdateColors();
            configFulfilledColor.SettingChanged += (s, e) => UpdateColors();
            configWishlistColor.SettingChanged += (s, e) => UpdateColors();
            configBarterColor.SettingChanged += (s, e) => UpdateColors();
            configCraftColor.SettingChanged += (s, e) => UpdateColors();
            configQuestPriority.SettingChanged += (s, e) => UpdatePriorities();
            configHideoutPriority.SettingChanged += (s, e) => UpdatePriorities();
            configWishlistPriority.SettingChanged += (s, e) => UpdatePriorities();
            configBarterPriority.SettingChanged += (s, e) => UpdatePriorities();
            configCraftPriority.SettingChanged += (s, e) => UpdatePriorities();

            // Initialize colors and priorities
            UpdateColors();
            UpdatePriorities();

            Logger.LogInfo("Configs loaded");
            Logger.LogInfo(
                $"Debug settings: hideoutLogs={debugHideoutTooltipLogs}, everyTooltip={debugLogEveryTooltip}, " +
                $"showFutureModulesLevels={showFutureModulesLevels}, fulfilledAnyCanBeUpgraded={fulfilledAnyCanBeUpgraded}");
        }

        private static void UpdateColors()
        {
            needMoreColor = configNeedMoreColor.Value;
            fulfilledColor = configFulfilledColor.Value;
            wishlistColor = configWishlistColor.Value;
            barterColor = configBarterColor.Value;
            craftColor = configCraftColor.Value;

            // Update the colors array
            colors[2] = wishlistColor;
            colors[3] = barterColor;
            colors[4] = craftColor;
        }

        private static void UpdatePriorities()
        {
            priorities[0] = questPriority;
            priorities[1] = hideoutPriority;
            priorities[2] = wishlistPriority;
            priorities[3] = barterPriority;
            priorities[4] = craftPriority;
        }

        private void LoadAssets()
        {
            var assetBundle = AssetBundle.LoadFromFile(modPath + "/MoreCheckmarksAssets");

            if (assetBundle == null)
            {
                LogError("Failed to load assets, inspect window checkmark may be miscolored");
            }
            else
            {
                whiteCheckmark = assetBundle.LoadAsset<Sprite>("WhiteCheckmark");
                benderBold = assetBundle.LoadAsset<TMP_FontAsset>("BenderBold");
                TMP_Text.OnFontAssetRequest += TMP_Text_onFontAssetRequest;
                LogInfo("Assets loaded");
            }
        }

        public static TMP_FontAsset TMP_Text_onFontAssetRequest(int hash, string name)
        {
            if (name.Equals("BENDERBOLD"))
            {
                return benderBold;
            }
            else
            {
                return null;
            }
        }

        private static void DoPatching()
        {
            const string profileTypeString = "Class308"; // Class303
            const string derivedTypeString = "Class1596"; // Class1470
            // Get assemblies
            Type profileSelector = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var t in assemblies)
            {
                if (t.GetName().Name.Equals("Assembly-CSharp"))
                {
                    // UPDATE: This is to know when a new profile is selected so we can load up to date data
                    // We want to do this when client makes request "/client/game/profile/select"
                    // Look for that string in dnspy, this creates a callback with a method_0, that is the method we want to postfix
                    profileSelector = t.GetType(profileTypeString).GetNestedType(derivedTypeString, BindingFlags.Public);
                }
            }

            var harmony = new Harmony(pluginGuid);
            harmony.PatchAll(); // Auto patch

            // Manual patch
            if (profileSelector != null)
            {
                var profileSelectorOriginal =
                    profileSelector.GetMethod("method_0", BindingFlags.Public | BindingFlags.Instance);

                var profileSelectorPostfix =
                    typeof(ProfileSelectionPatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(profileSelectorOriginal, null, new HarmonyMethod(profileSelectorPostfix));
            }
            else
            {
                LogError("Failed to Patch Profile Selector - Missing profileSelector");
            }
        }

        /// <summary>
        /// Как QuestItemViewPanel.smethod_0 — активный (Started) квест, для которого нужен предмет.
        /// </summary>
        public static bool TryGetActiveQuestCondition(Profile profile, Item item, out RawQuestClass questTemplate,
            out ConditionItem conditionItem)
        {
            questTemplate = null;
            conditionItem = null;
            if (profile?.QuestsData == null || item == null)
            {
                return false;
            }

            foreach (var questDataClass in profile.QuestsData)
            {
                if (questDataClass.Status != EQuestStatus.Started || questDataClass.Template == null)
                {
                    continue;
                }

                foreach (var kvp in questDataClass.Template.Conditions)
                {
                    foreach (Condition condition in kvp.Value)
                    {
                        if (questDataClass.CompletedConditions.Contains(condition.id))
                        {
                            continue;
                        }

                        if (condition is ConditionItem conditionItem2 &&
                            conditionItem2.target.Contains(item.StringTemplateId))
                        {
                            questTemplate = questDataClass.Template;
                            conditionItem = conditionItem2;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Зона убежища, где прямо сейчас можно начать/продолжить стройку (не Locked*).
        /// </summary>
        private static bool IsHideoutAreaActionable(AreaData ad)
        {
            switch (ad.Status)
            {
                case EAreaStatus.ReadyToConstruct:
                case EAreaStatus.ReadyToInstallConstruct:
                case EAreaStatus.ReadyToUpgrade:
                case EAreaStatus.ReadyToInstallUpgrade:
                    return true;
                default:
                    return false;
            }
        }

        public static NeededStruct GetNeeded(string itemTemplateID, ref List<string> areaNames)
        {
            var neededStruct = new NeededStruct
            {
                possessedCount = 0,
                requiredCount = 0
            };

            var debugThisItem = ShouldDebugItem(itemTemplateID);
            var totalAreas = 0;
            var skippedNullArea = 0;
            var skippedNoFutureUpgrades = 0;
            var skippedInProgress = 0;
            var skippedNotActionable = 0;
            var skippedNoStages = 0;
            var requirementsChecked = 0;
            var itemRequirementMatches = 0;

            try
            {
                var hideoutInstance = Singleton<HideoutClass>.Instance;
                if (hideoutInstance == null || hideoutInstance.AreaDatas == null)
                {
                    if (debugThisItem)
                    {
                        DebugLog($"[GetNeeded] item={itemTemplateID} hideoutInstance/AreaDatas is null");
                    }
                    return neededStruct;
                }

                totalAreas = hideoutInstance.AreaDatas.Count;
                foreach (var ad in hideoutInstance.AreaDatas)
                {
                    // Skip if don't have area data
                    if (ad == null || ad.Template == null || ad.Template.Name == null)
                    {
                        skippedNullArea++;
                        continue;
                    }

                    // Skip if the area has no future upgrades
                    if (ad.Status == EAreaStatus.NoFutureUpgrades)
                    {
                        skippedNoFutureUpgrades++;
                        continue;
                    }

                    // Стройка/апгрейд в процессе — требования следующего уровня пока не актуальны
                    if (ad.Status == EAreaStatus.Constructing || ad.Status == EAreaStatus.Upgrading)
                    {
                        skippedInProgress++;
                        continue;
                    }

                    // «Показывать будущие уровни» = ВЫКЛ → показываем только следующий уровень (даже если зона ещё Locked/NoFutureUpgrades).
                    // Ранее здесь стоял жёсткий фильтр IsHideoutAreaActionable, из-за которого при OFF вообще ничего не показывалось.
                    // Фильтр убран — логику "только следующий уровень" обеспечивает break ниже.

                    // Collect all future stages
                    var futureStages = new List<Stage>();
                    var lastStage = ad.CurrentStage;
                    while ((lastStage = ad.StageAt(lastStage.Level + 1)) != null && lastStage.Level != 0)
                    {
                        futureStages.Add(lastStage);

                        // If only want next level requirements, skip the rest
                        if (!showFutureModulesLevels)
                        {
                            break;
                        }
                    }

                    // Skip area if no stages were found to check requirements for
                    if (futureStages.Count == 0)
                    {
                        skippedNoStages++;
                        continue;
                    }

                    // Check requirements
                    foreach (var stage in futureStages)
                    {
                        var requirements = stage.Requirements;

                        try
                        {
                            foreach (var requirement in requirements)
                            {
                                requirementsChecked++;
                                if (!(requirement is ItemRequirement itemRequirement)) continue;
                                var requirementTemplate = itemRequirement.TemplateId;
                                if (itemTemplateID != requirementTemplate) continue;
                                itemRequirementMatches++;

                                // When showing only current actionable upgrades (showFutureModulesLevels = false),
                                // include ALL ReadyTo* zones that need this item, and show real per-zone progress (X/Y)
                                // instead of summing requirements across zones.
                                if (!showFutureModulesLevels)
                                {
                                    var zoneRequired = itemRequirement.IntCount;
                                    var zonePossessed = itemRequirement.UserItemsCount;

                                    // Track global needed/fulfilled flags for checkmark color
                                    if (requirement.Fulfilled)
                                    {
                                        if (!neededStruct.foundNeeded && !neededStruct.foundFulfilled)
                                        {
                                            neededStruct.foundFulfilled = true;
                                        }
                                        areaNames?.Add("<color=#" +
                                                       ColorUtility.ToHtmlStringRGB(fulfilledColor) + ">" +
                                                       ad.Template.Name +
                                                       " lvl" + stage.Level +
                                                       " (" + zonePossessed + "/" + zoneRequired + ")</color>");
                                    }
                                    else
                                    {
                                        if (!neededStruct.foundNeeded)
                                        {
                                            neededStruct.foundNeeded = true;
                                        }
                                        areaNames?.Add("<color=#" +
                                                       ColorUtility.ToHtmlStringRGB(needMoreColor) + ">" +
                                                       ad.Template.Name +
                                                       " lvl" + stage.Level +
                                                       " (" + zonePossessed + "/" + zoneRequired + ")</color>");
                                    }

                                    // Keep first zone's numbers for backward compatibility (used by color logic elsewhere)
                                    if (neededStruct.requiredCount == 0)
                                    {
                                        neededStruct.requiredCount = zoneRequired;
                                        neededStruct.possessedCount = zonePossessed;
                                    }
                                    continue;
                                }

                                // Sum up the total amount of this item required in entire hideout and update possessed amount
                                neededStruct.requiredCount += itemRequirement.IntCount;
                                neededStruct.possessedCount = itemRequirement.UserItemsCount;

                                // A requirement but already have the amount we need
                                if (requirement.Fulfilled)
                                {
                                    // Even if we have enough of this item to fulfill a requirement in one area
                                    // we might still need it, and if thats the case we want to show that color, not fulfilled color, so you know you still need more of it
                                    // So only set color to fulfilled if not needed
                                    if (!neededStruct.foundNeeded && !neededStruct.foundFulfilled)
                                    {
                                        neededStruct.foundFulfilled = true;
                                    }

                                    areaNames?.Add("<color=#" +
                                                   ColorUtility.ToHtmlStringRGB(fulfilledColor) + ">" +
                                                   ad.Template.Name +
                                                   " lvl" + stage.Level + "</color>");
                                }
                                else
                                {
                                    if (!neededStruct.foundNeeded)
                                    {
                                        neededStruct.foundNeeded = true;
                                    }

                                    areaNames?.Add("<color=#" +
                                                   ColorUtility.ToHtmlStringRGB(needMoreColor) + ">" +
                                                   ad.Template.Name +
                                                   " lvl" + stage.Level + "</color>");
                                }
                            }
                        }
                        catch (Exception)
                        {
                            LogError("Failed to get whether item " + itemTemplateID +
                                     " was needed for hideout area: " + ad.Template.Name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError("Failed to find out if item needed for upgrade - - - -" + e.StackTrace);
                LogError("Failed to get whether item " + itemTemplateID + " was needed for hideout upgrades.");
            }

            if (debugThisItem)
            {
                DebugLog(
                    $"[GetNeeded] item={itemTemplateID} showFutureModulesLevels={showFutureModulesLevels} " +
                    $"areasTotal={totalAreas} skippedNull={skippedNullArea} skippedNoFuture={skippedNoFutureUpgrades} " +
                    $"skippedInProgress={skippedInProgress} skippedNotActionable={skippedNotActionable} " +
                    $"skippedNoStages={skippedNoStages} requirementsChecked={requirementsChecked} " +
                    $"matches={itemRequirementMatches} areaNames={areaNames?.Count ?? 0} " +
                    $"required={neededStruct.requiredCount} possessed={neededStruct.possessedCount} " +
                    $"foundNeeded={neededStruct.foundNeeded} foundFulfilled={neededStruct.foundFulfilled}");
            }

            return neededStruct;
        }

        public static bool GetNeededCraft(string itemTemplateID, ref string tooltip, bool needTooltip = true)
        {
            bool required = false;
            bool gotTooltip = false;
            try
            {
                HideoutClass hideoutInstance = Singleton<HideoutClass>.Instance;
                foreach (AreaData ad in hideoutInstance.AreaDatas)
                {
                    // Skip if don't have area data
                    if (ad == null || ad.Template == null || ad.Template.Name == null)
                    {
                        continue;
                    }

                    // Get stage to check productions of
                    // Productions are cumulative, a stage will have productions of all previous stages
                    Stage currentStage = ad.CurrentStage;
                    if (currentStage == null)
                    {
                        int level = 0;
                        while (currentStage == null)
                        {
                            currentStage = ad.StageAt(level++);
                        }
                    }

                    if (currentStage != null)
                    {
                        Stage newStage = ad.StageAt(currentStage.Level + 1);
                        while (newStage != null && newStage.Level != 0)
                        {
                            if (newStage.Level > ad.CurrentLevel && !showFutureCraft)
                            {
                                break;
                            }

                            currentStage = newStage;
                            newStage = ad.StageAt(currentStage.Level + 1);
                        }
                    }

                    if (currentStage == null)
                    {
                        continue;
                    }

                    // UPDATE: Class here is class used in AreaData.Stage.Production.Data array
                    if (currentStage.Production != null && currentStage.Production.Data != null)
                    {
                        bool areaNameAdded = false;
                        foreach (ProductionBuildAbstractClass productionData in currentStage.Production.Data)
                        {
                            Requirement[] requirements = productionData.requirements;

                            foreach (Requirement baseReq in requirements)
                            {
                                if (baseReq.Type == ERequirementType.Item)
                                {
                                    ItemRequirement itemRequirement = baseReq as ItemRequirement;

                                    if (itemTemplateID == itemRequirement.TemplateId)
                                    {
                                        required = true;

                                        if (needTooltip)
                                        {
                                            if (productionEndProductByID.TryGetValue(productionData._id,
                                                    out string product))
                                            {
                                                gotTooltip = true;
                                                if (!areaNameAdded)
                                                {
                                                    tooltip += "\n  " + ad.Template.Name.Localized();
                                                    areaNameAdded = true;
                                                }

                                                tooltip += "\n    <color=#" + ColorUtility.ToHtmlStringRGB(craftColor) +
                                                           ">" + (product + " Name").Localized() + " lvl" +
                                                           productionData.Level + "</color> (" +
                                                           itemRequirement.IntCount + ")";
                                            }
                                        }
                                        else
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to get whether item " + itemTemplateID +
                         " was needed for crafting: " + ex.Message);
            }

            return required && gotTooltip;
        }

        public static List<List<KeyValuePair<string, int>>> GetBarters(string ID)
        {
            var bartersByTrader = new List<List<KeyValuePair<string, int>>>();

            if (showBarter)
            {
                for (var i = 0; i < bartersByItemByTrader.Count; ++i)
                {
                    List<KeyValuePair<string, int>> current = null;

                    bartersByItemByTrader[i]?.TryGetValue(ID, out current);

                    if (current == null)
                    {
                        current = new List<KeyValuePair<string, int>>();
                    }

                    bartersByTrader.Add(current);
                }
            }

            return bartersByTrader;
        }

        /// <summary>
        /// Gets all prerequisite quest IDs for a given quest (recursive, with caching)
        /// </summary>
        public static HashSet<string> GetAllPrerequisites(string questId)
        {
            // Return cached result if available
            if (prereqCache.TryGetValue(questId, out var cached))
                return cached;

            var result = new HashSet<string>();
            var queue = new Queue<string>();

            // Add direct prerequisites to queue
            if (questPrerequisites.TryGetValue(questId, out var directPrereqs))
            {
                foreach (var prereq in directPrereqs)
                    queue.Enqueue(prereq);
            }

            // BFS to find all prerequisites
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (result.Contains(current)) continue;

                result.Add(current);

                if (questPrerequisites.TryGetValue(current, out var prereqs))
                {
                    foreach (var prereq in prereqs)
                    {
                        if (!result.Contains(prereq))
                            queue.Enqueue(prereq);
                    }
                }
            }

            // Cache and return
            prereqCache[questId] = result;
            return result;
        }

        /// <summary>
        /// Gets the count of remaining (incomplete) prerequisite quests
        /// </summary>
        public static int GetRemainingPrerequisiteCount(string questId, Profile profile)
        {
            var allPrereqs = GetAllPrerequisites(questId);
            int remaining = 0;

            foreach (var prereqId in allPrereqs)
            {
                // Use the cached IsQuestCompleted check
                if (!IsQuestCompleted(prereqId, profile))
                {
                    remaining++;
                }
            }

            return remaining;
        }

        /// <summary>
        /// Checks if a quest is completed (Success status). Caches true results since quests can't be un-completed.
        /// </summary>
        public static bool IsQuestCompleted(string questId, Profile profile)
        {
            // Check cache first - if we've seen this quest completed before, it's still completed
            if (completedQuestIds.Contains(questId))
                return true;

            foreach (var questDataClass in profile.QuestsData)
            {
                if (questDataClass.Template != null && questDataClass.Template.Id == questId)
                {
                    if (questDataClass.Status == EQuestStatus.Success)
                    {
                        // Cache the result - quest completion is permanent
                        completedQuestIds.Add(questId);
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the prerequisite status string for display in tooltip (uses pre-computed count)
        /// </summary>
        public static string GetPrerequisiteStatusString(int remaining)
        {
            // If feature is disabled, return empty string
            if (!showPrerequisiteQuests)
            {
                return "";
            }

            if (remaining == 0)
            {
                // Green for available quests
                return " <color=#00ff00>(0 предв. квестов)</color>";
            }
            else if (remaining < 10)
            {
                // Yellow for quests with few prerequisites (1-9)
                return $" <color=#ffff00>({remaining} предв. квест{(remaining == 1 ? "" : "ов")})</color>";
            }
            else
            {
                // Gray for quests with many prerequisites (10+)
                return $" <color=#888888>({remaining} предв. квестов)</color>";
            }
        }

        public static void LogInfo(string msg)
        {
            modInstance.Logger.LogInfo(msg);
        }

        public static void DebugLog(string msg)
        {
            if (modInstance == null || !debugHideoutTooltipLogs)
            {
                return;
            }

            modInstance.Logger.LogInfo("[DEBUG] " + msg);
        }

        public static bool ShouldDebugItem(string itemTemplateID)
        {
            if (!debugHideoutTooltipLogs)
            {
                return false;
            }

            if (debugLogEveryTooltip || string.IsNullOrEmpty(itemTemplateID))
            {
                return true;
            }

            if (debugLoggedItemTemplates.Count > 5000)
            {
                debugLoggedItemTemplates.Clear();
            }

            return debugLoggedItemTemplates.Add(itemTemplateID);
        }

        public static void LogError(string msg)
        {
            modInstance.Logger.LogError(msg);
        }
    }

    [HarmonyPatch]
    class QuestItemViewPanelShowPatch
    {
        // Replaces the original QuestItemViewPanel.Show() to use custom checkmark colors and tooltips
        [HarmonyPatch(typeof(QuestItemViewPanel), nameof(QuestItemViewPanel.Show))]
        static bool Prefix(
            Profile profile,
            Item item,
            SimpleTooltip tooltip,
            QuestItemViewPanel __instance,
            ref Image ____questIconImage,
            ref Sprite ____foundInRaidSprite,
            ref string ___string_5,
            ref SimpleTooltip ___simpleTooltip_0,
            TextMeshProUGUI ____questItemLabel)
        {
            try
            {
                var possessedCount = 0;
                var possessedQuestCount = 0;

                __instance.HideGameObject();

                if (profile != null)
                {
                    var inventoryItems = Singleton<HideoutClass>.Instance.AllStashItems.Where(x => x.TemplateId == item.TemplateId);
                    foreach (var currentItem in inventoryItems)
                    {
                        if (currentItem.MarkedAsSpawnedInSession)
                        {
                            possessedQuestCount += currentItem.StackObjectsCount;
                        }

                        possessedCount += currentItem.StackObjectsCount;
                    }
                }
                else
                {
                    MoreCheckmarksMod.LogError("Profile null for item " + item.Template.Name);
                }

                var areaNames = new List<string>();
                var neededStruct = MoreCheckmarksMod.GetNeeded(item.TemplateId, ref areaNames);
                if (MoreCheckmarksMod.debugHideoutTooltipLogs)
                {
                    MoreCheckmarksMod.DebugLog(
                        $"[Tooltip:Show] item={item.TemplateId} name='{item.Template?.Name}' " +
                        $"areaNames={areaNames.Count} needed={neededStruct.foundNeeded} fulfilled={neededStruct.foundFulfilled} " +
                        $"required={neededStruct.requiredCount} possessed={neededStruct.possessedCount}");
                }
                MoreCheckmarksMod.questDataStartByItemTemplateID.TryGetValue(item.TemplateId,
                    out var startQuests);
                MoreCheckmarksMod.questDataCompleteByItemTemplateID.TryGetValue(item.TemplateId,
                    out var completeQuests);

                var wishlist = ItemUiContext.Instance.WishlistManager.IsInWishlist(item.TemplateId, true, out _);

                var craftTooltip = "";
                var craftRequired = MoreCheckmarksMod.showCraft &&
                                     MoreCheckmarksMod.GetNeededCraft(item.TemplateId, ref craftTooltip);

                var hasServerQuestData = (startQuests != null && startQuests.questData.Count > 0) ||
                                         (completeQuests != null && completeQuests.questData.Count > 0);
                var hasActiveQuest = MoreCheckmarksMod.TryGetActiveQuestCondition(profile, item, out _, out _);

                var questItem = (item.MarkedAsSpawnedInSession || MoreCheckmarksMod.showQuestCheckmarksNonFIR) &&
                                (hasActiveQuest ||
                                 (MoreCheckmarksMod.includeFutureQuests && hasServerQuestData));


                if (____questItemLabel != null)
                {
                    // Since being quest item could be set by future quests, need to make sure we have "QUEST ITEM" label
                    if (questItem)
                    {
                        ____questItemLabel.text = "ПРЕДМЕТ КВЕСТА";
                    }

                    ____questItemLabel.gameObject.SetActive(questItem);
                }

                var bartersByTrader = MoreCheckmarksMod.GetBarters(item.TemplateId);
                var gotBarters = false;
                if (bartersByTrader != null)
                {
                    if (bartersByTrader.Any(t => t != null && t.Count > 0))
                    {
                        gotBarters = true;
                    }
                }

                MoreCheckmarksMod.neededFor[0] = questItem;
                MoreCheckmarksMod.neededFor[1] = neededStruct.foundNeeded || neededStruct.foundFulfilled;
                MoreCheckmarksMod.neededFor[2] = wishlist;
                MoreCheckmarksMod.neededFor[3] = gotBarters;
                MoreCheckmarksMod.neededFor[4] = craftRequired;

                var currentNeeded = -1;
                var currentHighest = -1;

                for (var i = 0; i < 5; ++i)
                {
                    if (!MoreCheckmarksMod.neededFor[i] || MoreCheckmarksMod.priorities[i] <= currentHighest) continue;
                    currentNeeded = i;
                    currentHighest = MoreCheckmarksMod.priorities[i];
                }

                if (currentNeeded > -1)
                {
                    // Handle special case of areas
                    if (currentNeeded == 1)
                    {
                        if (neededStruct.foundNeeded) // Need more
                        {
                            SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite,
                                MoreCheckmarksMod.needMoreColor);
                        }
                        else if (neededStruct.foundFulfilled) // We have enough for at least one upgrade
                        {
                            if (MoreCheckmarksMod
                                .fulfilledAnyCanBeUpgraded) // We want to know when have enough for at least one upgrade
                            {
                                SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite,
                                    MoreCheckmarksMod.fulfilledColor);
                            }
                            else // We only want fulfilled checkmark when ALL requiring this item can be upgraded
                            {
                                // Check if we truly do not need more of this item for now
                                if (neededStruct.possessedCount >= neededStruct.requiredCount)
                                {
                                    SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite,
                                        MoreCheckmarksMod.fulfilledColor);
                                }
                                else // Still need more
                                {
                                    SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite,
                                        MoreCheckmarksMod.needMoreColor);
                                }
                            }
                        }
                    }
                    else // Not area, just set color
                    {
                        SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite,
                            MoreCheckmarksMod.colors[currentNeeded]);
                    }
                }
                else if (item.MarkedAsSpawnedInSession) // Item not needed for anything but found in raid
                {
                    SetCheckmark(__instance, ____questIconImage, ____foundInRaidSprite, Color.white);
                }

                SetTooltip(profile, areaNames, ref ___string_5, ref ___simpleTooltip_0, ref tooltip, item, startQuests,
                    completeQuests, possessedCount, possessedQuestCount, neededStruct.requiredCount, wishlist,
                    bartersByTrader, gotBarters, craftRequired, craftTooltip);

                return false;
            }
            catch (Exception e)
            {
                MoreCheckmarksMod.LogError(
                    "Failed to show checkmark for item " + item.Template.Name + " - " + e.Message);
                return true;
            }
        }

        private static void SetCheckmark(QuestItemViewPanel instance, Image questIconImage,
            Sprite sprite, Color color)
        {
            try
            {
                // Following calls base class method ShowGameObject()
                instance.ShowGameObject();
                questIconImage.sprite = sprite;
                questIconImage.color = color;
            }
            catch
            {
                MoreCheckmarksMod.LogError("SetCheckmark failed");
            }
        }

        private static void SetTooltip(Profile profile, List<string> areaNames, ref string ___string_5,
            ref SimpleTooltip ___simpleTooltip_0, ref SimpleTooltip tooltip,
            Item item, MoreCheckmarksMod.QuestPair startQuests,
            MoreCheckmarksMod.QuestPair completeQuests,
            int possessedCount, int possessedQuestCount, int requiredCount, bool wishlist,
            List<List<KeyValuePair<string, int>>> bartersByTrader, bool gotBarters,
            bool craftRequired, string craftTooltip)
        {
            try
            {
                // Reset string
                ___string_5 = "Схрон" + ": <color=#dd831a>" + possessedQuestCount + "</color>/" +
                              possessedCount;

                // Show found in raid if found in raid
                if (item.MarkedAsSpawnedInSession)
                {
                    ___string_5 += "\n" + "Предмет найден в рейде";
                }

                // Add quests
                var gotQuest = false;
                if (item.MarkedAsSpawnedInSession || MoreCheckmarksMod.showQuestCheckmarksNonFIR)
                {
                    if (MoreCheckmarksMod.includeFutureQuests)
                    {
                        var questStartString = "<color=#dd831a>";
                        var gotStartQuests = false;
                        var gotMoreThanOneStartQuest = false;
                        var totalItemCount = 0;
                        if (startQuests != null)
                        {
                            // Filter out completed quests
                            var filteredStartQuests = startQuests.questData
                                .Where(q => !MoreCheckmarksMod.IsQuestCompleted(q.Value.questId, profile));

                            // Only compute and sort by prereq counts if feature is enabled
                            var startQuestsWithPrereqs = MoreCheckmarksMod.showPrerequisiteQuests
                                ? filteredStartQuests
                                    .Select(q => new { Entry = q, PrereqCount = MoreCheckmarksMod.GetRemainingPrerequisiteCount(q.Value.questId, profile) })
                                    .OrderBy(q => q.PrereqCount)
                                    .ToList()
                                : filteredStartQuests
                                    .Select(q => new { Entry = q, PrereqCount = 0 })
                                    .ToList();
                            var count = startQuestsWithPrereqs.Count;

                            if (count > 0)
                            {
                                gotStartQuests = true;
                                totalItemCount = startQuests.count;
                            }

                            if (count > 1)
                            {
                                gotMoreThanOneStartQuest = true;
                            }

                            var index = 0;
                            foreach (var questWithPrereq in startQuestsWithPrereqs)
                            {
                                var questEntry = questWithPrereq.Entry;
                                var localizedName = questEntry.Key.Localized(null);
                                if (questEntry.Key.Equals(localizedName))
                                {
                                    // Could not localize name, just use default name
                                    if (string.IsNullOrEmpty(questEntry.Value.questName))
                                    {
                                        questStartString += "Неизвестный квест";
                                    }
                                    else
                                    {
                                        questStartString += questEntry.Value.questName;
                                    }
                                }
                                else
                                {
                                    questStartString += localizedName;
                                }

                                // Add prerequisite status (using pre-computed count, empty string if disabled)
                                questStartString += MoreCheckmarksMod.GetPrerequisiteStatusString(questWithPrereq.PrereqCount);

                                if (index != count - 1)
                                {
                                    questStartString += ",\n  ";
                                }
                                else
                                {
                                    questStartString += "</color>";
                                }

                                ++index;
                            }
                        }

                        if (gotStartQuests)
                        {
                            gotQuest = true;
                            ___string_5 = "\nНужно (" + possessedQuestCount + "/" + totalItemCount +
                                          ") для начала квест" + (gotMoreThanOneStartQuest ? "ов" : "а") + ":\n  " +
                                          questStartString;
                        }

                        var questCompleteString = "<color=#dd831a>";
                        var gotCompleteQuests = false;
                        var gotMoreThanOneCompleteQuest = false;
                        if (completeQuests != null)
                        {
                            // Filter out completed quests
                            var filteredCompleteQuests = completeQuests.questData
                                .Where(q => !MoreCheckmarksMod.IsQuestCompleted(q.Value.questId, profile));

                            // Only compute and sort by prereq counts if feature is enabled
                            var completeQuestsWithPrereqs = MoreCheckmarksMod.showPrerequisiteQuests
                                ? filteredCompleteQuests
                                    .Select(q => new { Entry = q, PrereqCount = MoreCheckmarksMod.GetRemainingPrerequisiteCount(q.Value.questId, profile) })
                                    .OrderBy(q => q.PrereqCount)
                                    .ToList()
                                : filteredCompleteQuests
                                    .Select(q => new { Entry = q, PrereqCount = 0 })
                                    .ToList();
                            var count = completeQuestsWithPrereqs.Count;

                            if (count > 0)
                            {
                                gotCompleteQuests = true;
                                totalItemCount = completeQuests.count;
                            }

                            if (count > 1)
                            {
                                gotMoreThanOneCompleteQuest = true;
                            }

                            var index = 0;
                            foreach (var questWithPrereq in completeQuestsWithPrereqs)
                            {
                                var questEntry = questWithPrereq.Entry;
                                var localizedName = questEntry.Key.Localized(null);
                                if (questEntry.Key.Equals(localizedName))
                                {
                                    // Could not localize name, just use default name
                                    if (string.IsNullOrEmpty(questEntry.Value.questName))
                                    {
                                        questCompleteString += "Неизвестный квест";
                                    }
                                    else
                                    {
                                        questCompleteString += questEntry.Value.questName;
                                    }
                                }
                                else
                                {
                                    questCompleteString += localizedName;
                                }

                                // Add prerequisite status (using pre-computed count, empty string if disabled)
                                questCompleteString += MoreCheckmarksMod.GetPrerequisiteStatusString(questWithPrereq.PrereqCount);

                                if (index != count - 1)
                                {
                                    questCompleteString += ",\n  ";
                                }
                                else
                                {
                                    questCompleteString += "</color>";
                                }

                                ++index;
                            }
                        }

                        if (gotCompleteQuests)
                        {
                            gotQuest = true;
                            ___string_5 += "\nНужно (" + possessedQuestCount + "/" + totalItemCount +
                                           ") для завершения квест" + (gotMoreThanOneCompleteQuest ? "ов" : "а") + ":\n  " +
                                           questCompleteString;
                        }
                    }
                    else // Don't include future quests, do as vanilla
                    {
                        if (MoreCheckmarksMod.TryGetActiveQuestCondition(profile, item, out var rawQuestClass,
                                out var conditionItem) && rawQuestClass != null)
                        {
                            string arg = "<color=#dd831a>" + rawQuestClass.Name + "</color>";
                            if (item.QuestItem)
                            {
                                gotQuest = true;
                                ___string_5 += string.Format("\nПредмет относится к активному квесту {0}",
                                    arg);
                            }

                            Weapon weapon;
                            ConditionWeaponAssembly condition;
                            if (!gotQuest && (weapon = (item as Weapon)) != null &&
                                (condition = (conditionItem as ConditionWeaponAssembly)) != null &&
                                Inventory.IsWeaponFitsCondition(weapon, condition, false))
                            {
                                gotQuest = true;
                                ___string_5 +=
                                    string.Format("\nПредмет подходит под требования активного квеста {0}", arg);
                            }

                            if (!gotQuest && item.MarkedAsSpawnedInSession)
                            {
                                gotQuest = true;
                                ___string_5 +=
                                    string.Format(
                                        "\nПредмет был найден в рейде для задания {0}", arg);
                            }
                        }
                    }
                }

                // Add areas
                var gotAreas = areaNames.Count > 0;
                var areaNamesString = "";
                for (var i = 0; i < areaNames.Count; ++i)
                {
                    areaNamesString += "\n  " + areaNames[i];
                }

                if (MoreCheckmarksMod.debugHideoutTooltipLogs)
                {
                    MoreCheckmarksMod.DebugLog(
                        $"[Tooltip:SetTooltip] item={item?.TemplateId} gotAreas={gotAreas} areaCount={areaNames.Count} " +
                        $"showFutureModulesLevels={MoreCheckmarksMod.showFutureModulesLevels} " +
                        $"required={requiredCount} possessed={possessedCount}");
                }

                if (!areaNamesString.Equals(""))
                {
                    // When areaNames already contain per-zone progress "(X/Y)", omit the global header numbers
                    // to avoid duplication (used in "show only current actionable upgrades" mode).
                    var firstArea = areaNames.Count > 0 ? areaNames[0] : "";
                    if (firstArea.Contains("("))
                    {
                        ___string_5 +=
                            string.Format("\nНужно для област" + (areaNames.Count == 1 ? "и" : "ей") + ":{0}",
                                areaNamesString);
                    }
                    else
                    {
                        ___string_5 +=
                            string.Format("\nНужно ({1}/{2}) для област" + (areaNames.Count == 1 ? "и" : "ей") + ":{0}",
                                areaNamesString, possessedCount, requiredCount);
                    }
                }

                // Add wishlist
                if (wishlist)
                {
                    ___string_5 += string.Format("\nВ {0}",
                        "<color=#" + ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod.wishlistColor) +
                        ">Список желаемого</color>");
                }

                // Add craft
                if (craftRequired)
                {
                    ___string_5 += string.Format("\nНужно для крафта:{0}", craftTooltip);
                }

                // Add barters
                if (gotBarters)
                {
                    var firstBarter = false;
                    if (bartersByTrader != null)
                    {
                        for (var i = 0; i < bartersByTrader.Count; ++i)
                        {
                            if (bartersByTrader[i] != null && bartersByTrader[i].Count > 0)
                            {
                                if (!firstBarter)
                                {
                                    ___string_5 += "\n" + "Бартер" + ":";
                                    firstBarter = true;
                                }

                                var bartersString = "\n У " + (MoreCheckmarksMod.traders.Length > i
                                    ? MoreCheckmarksMod.traders[i]
                                    : "Торговец " + i) + ":";
                                for (var j = 0; j < bartersByTrader[i].Count; ++j)
                                {
                                    bartersString += "\n  <color=#" +
                                                     ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod.barterColor) + ">" +
                                                     (bartersByTrader[i][j].Key + " Name").Localized() + "</color> (" +
                                                     bartersByTrader[i][j].Value + ")";
                                }

                                ___string_5 += bartersString;
                            }
                        }
                    }
                }

                if (gotQuest || gotAreas || wishlist || gotBarters || craftRequired || item.MarkedAsSpawnedInSession)
                {
                    // If this is not a quest item or found in raid, the original returns and the tooltip never gets set, so we need to set it ourselves
                    ___simpleTooltip_0 = tooltip;
                }
            }
            catch
            {
                MoreCheckmarksMod.LogError("SetToolTip failed");
            }
        }
    }

    [HarmonyPatch]
    class ItemSpecificationPanelShowPatch
    {
        // This postfix will run after the inspect window sets its checkmark if there is one
        // If there is one, the postfix for the QuestItemViewPanel will always have run before
        // This patch just changes the sprite to a default white one so we can set its color to whatever we need
        [HarmonyPatch(typeof(ItemSpecificationPanel), "method_2")]
        static void Postfix(ref Item ___item_0, ref QuestItemViewPanel ____questItemViewPanel)
        {
            try
            {
                // If the checkmark exists and if the color of the checkmark is custom
                if (____questItemViewPanel != null)
                {
                    // Get access to QuestItemViewPanel's private _questIconImage
                    var bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Static;
                    var iconImageField = typeof(QuestItemViewPanel).GetField("_questIconImage", bindFlags);
                    var _questIconImage = iconImageField.GetValue(____questItemViewPanel) as Image;

                    if (_questIconImage != null)
                    {
                        _questIconImage.sprite = MoreCheckmarksMod.whiteCheckmark;
                    }
                }
            }
            catch
            {
                MoreCheckmarksMod.LogError("ItemSpecificationPanelShowPatch failed");
            }
        }
    }

    [HarmonyPatch]
    class AvailableActionsPatch
    {
        // This postfix will run after we get a list of all actions available to interact with the item we are pointing at
        [HarmonyPatch(typeof(GetActionsClass), "smethod_8")]
        static void Postfix(GamePlayerOwner owner, LootItem lootItem, ref ActionsReturnClass __result)
        {
            try
            {
                foreach (ActionsTypesClass action in __result.Actions)
                {
                    if (action.Name.Equals("Take"))
                    {
                        List<string> nullAreaNames = null;
                        NeededStruct neededStruct = MoreCheckmarksMod.GetNeeded(lootItem.TemplateId, ref nullAreaNames);
                        string craftTooltip = "";
                        bool craftRequired =
                            MoreCheckmarksMod.GetNeededCraft(lootItem.TemplateId, ref craftTooltip, false);
                        bool wishlist =
                            ItemUiContext.Instance.WishlistManager.IsInWishlist(lootItem.TemplateId, true,
                                out EWishlistGroup group);
                        MoreCheckmarksMod.questDataStartByItemTemplateID.TryGetValue(lootItem.TemplateId,
                            out MoreCheckmarksMod.QuestPair startQuests);
                        MoreCheckmarksMod.questDataCompleteByItemTemplateID.TryGetValue(lootItem.TemplateId,
                            out MoreCheckmarksMod.QuestPair completeQuests);
                        var lootProfile = owner?.Player?.Profile;
                        var hasServerQuestData = (startQuests != null && startQuests.questData.Count > 0) ||
                                                 (completeQuests != null && completeQuests.questData.Count > 0);
                        var hasActiveQuest = lootProfile != null &&
                                             MoreCheckmarksMod.TryGetActiveQuestCondition(lootProfile, lootItem.Item,
                                                 out _, out _);
                        bool questItem = (lootItem.Item.MarkedAsSpawnedInSession ||
                                          MoreCheckmarksMod.showQuestCheckmarksNonFIR) &&
                                         (hasActiveQuest ||
                                          (MoreCheckmarksMod.includeFutureQuests && hasServerQuestData));
                        List<List<KeyValuePair<string, int>>> bartersByTrader =
                            MoreCheckmarksMod.GetBarters(lootItem.TemplateId);
                        bool gotBarters = false;
                        if (bartersByTrader != null)
                        {
                            for (int i = 0; i < bartersByTrader.Count; ++i)
                            {
                                if (bartersByTrader[i] != null && bartersByTrader[i].Count > 0)
                                {
                                    gotBarters = true;
                                    break;
                                }
                            }
                        }

                        bool[] currentNeededFor =
                        {
                            questItem, neededStruct.foundNeeded || neededStruct.foundFulfilled, wishlist, gotBarters,
                            craftRequired
                        };
                        // Find needed with highest priority
                        int currentNeeded = -1;
                        int currentHighest = -1;
                        for (int i = 0; i < 5; ++i)
                        {
                            if (currentNeededFor[i] && MoreCheckmarksMod.priorities[i] > currentHighest)
                            {
                                currentNeeded = i;
                                currentHighest = MoreCheckmarksMod.priorities[i];
                            }
                        }

                        if (currentNeeded != -1)
                        {
                            // Handle special case of areas
                            if (currentNeeded == 1)
                            {
                                if (neededStruct.foundNeeded) // Need more
                                {
                                    action.Name = "<font=\"BenderBold\"><color=#" +
                                                  ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod.needMoreColor) +
                                                  ">Взять</color></font>";
                                }
                                else if (neededStruct.foundFulfilled) // We have enough for at least one upgrade
                                {
                                    if (MoreCheckmarksMod
                                        .fulfilledAnyCanBeUpgraded) // We want to know when have enough for at least one upgrade
                                    {
                                        action.Name = "<font=\"BenderBold\"><color=#" +
                                                      ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod.fulfilledColor) +
                                                      ">Взять</color></font>";
                                    }
                                    else // We only want fulfilled checkmark when ALL requiring this item can be upgraded
                                    {
                                        // Check if we trully do not need more of this item for now
                                        if (neededStruct.possessedCount >= neededStruct.requiredCount)
                                        {
                                            action.Name = "<font=\"BenderBold\"><color=#" +
                                                          ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod
                                                              .fulfilledColor) + ">Взять</color></font>";
                                        }
                                        else // Still need more
                                        {
                                            action.Name = "<font=\"BenderBold\"><color=#" +
                                                          ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod
                                                              .needMoreColor) + ">Взять</color></font>";
                                        }
                                    }
                                }
                            }
                            else // Not area, just set color
                            {
                                MoreCheckmarksMod.LogInfo("TAKE NEEDED, NOT FOR AREA, COLORING FOR NEEDED FOR INDEX " +
                                                          currentNeeded + " AS COLOR " +
                                                          ColorUtility.ToHtmlStringRGB(
                                                              MoreCheckmarksMod.colors[currentNeeded]));
                                action.Name = "<font=\"BenderBold\"><color=#" +
                                              ColorUtility.ToHtmlStringRGB(MoreCheckmarksMod.colors[currentNeeded]) +
                                              ">Взять</color></font>";
                            }
                        }
                        //else leave it as it is

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MoreCheckmarksMod.LogError("Failed to process available actions for loose item: " + ex.Message + "\n" +
                                           ex.StackTrace);
            }
        }
    }


    [HarmonyPatch]
    class QuestClassStatusPatch
    {
        private static EQuestStatus preStatus;

        // This prefix will run before a quest's status has been set
        [HarmonyPatch(typeof(QuestClass), "SetStatus")]
        static void Prefix(QuestClass __instance)
        {
            preStatus = __instance.QuestStatus;
        }

        // This postfix will run after a quest's status has been set
        [HarmonyPatch(typeof(QuestClass), "SetStatus")]
        static void Postfix(QuestClass __instance)
        {
            if (__instance == null)
            {
                MoreCheckmarksMod.LogError("Attempted setting queststatus but instance is null");
                return;
            }

            if (__instance.Template == null)
            {
                return;
            }

            MoreCheckmarksMod.LogInfo("Quest " + __instance.Template.Name + " queststatus set to " +
                                      __instance.QuestStatus);

            try
            {
                if (__instance.QuestStatus != preStatus)
                {
                    switch (__instance.QuestStatus)
                    {
                        case EQuestStatus.Started:
                            if (preStatus == EQuestStatus.AvailableForStart)
                            {
                                if (MoreCheckmarksMod.neededStartItemsByQuest.TryGetValue(__instance.Template.Id,
                                        out Dictionary<string, int> startItems))
                                {
                                    foreach (KeyValuePair<string, int> itemEntry in startItems)
                                    {
                                        if (MoreCheckmarksMod.questDataStartByItemTemplateID.TryGetValue(itemEntry.Key,
                                                out MoreCheckmarksMod.QuestPair questList))
                                        {
                                            // Find the key that matches this quest ID (key is locale name, value contains questId)
                                            string keyToRemove = null;
                                            foreach (var kvp in questList.questData)
                                            {
                                                if (kvp.Value.questId == __instance.Template.Id)
                                                {
                                                    keyToRemove = kvp.Key;
                                                    break;
                                                }
                                            }
                                            if (keyToRemove != null)
                                            {
                                                questList.questData.Remove(keyToRemove);
                                                questList.count -= itemEntry.Value;
                                                if (questList.questData.Count == 0)
                                                {
                                                    MoreCheckmarksMod.questDataStartByItemTemplateID.Remove(itemEntry.Key);
                                                }
                                            }
                                        }
                                    }

                                    MoreCheckmarksMod.neededStartItemsByQuest.Remove(__instance.Template.Id);
                                }
                            }

                            // If quest data was incomplete (profile wasn't ready), reload now that a quest has been accepted
                            if (MoreCheckmarksMod.questDataNeedsReload)
                            {
                                MoreCheckmarksMod.LogInfo("Quest accepted - reloading quest data");
                                MoreCheckmarksMod.modInstance.LoadData();
                            }

                            break;
                        case EQuestStatus.Success:
                        case EQuestStatus.Expired:
                        case EQuestStatus.Fail:
                            if (MoreCheckmarksMod.neededCompleteItemsByQuest.TryGetValue(__instance.Template.Id,
                                    out Dictionary<string, int> completeItems))
                            {
                                foreach (KeyValuePair<string, int> itemEntry in completeItems)
                                {
                                    if (MoreCheckmarksMod.questDataCompleteByItemTemplateID.TryGetValue(itemEntry.Key,
                                            out MoreCheckmarksMod.QuestPair questList))
                                    {
                                        // Find the key that matches this quest ID (key is locale name, value contains questId)
                                        string keyToRemove = null;
                                        foreach (var kvp in questList.questData)
                                        {
                                            if (kvp.Value.questId == __instance.Template.Id)
                                            {
                                                keyToRemove = kvp.Key;
                                                break;
                                            }
                                        }
                                        if (keyToRemove != null)
                                        {
                                            questList.questData.Remove(keyToRemove);
                                            questList.count -= itemEntry.Value;
                                            if (questList.questData.Count == 0)
                                            {
                                                MoreCheckmarksMod.questDataCompleteByItemTemplateID.Remove(itemEntry.Key);
                                            }
                                        }
                                    }
                                }

                                MoreCheckmarksMod.neededCompleteItemsByQuest.Remove(__instance.Template.Id);
                            }

                            // Also add to completed quests cache for immediate filtering
                            MoreCheckmarksMod.completedQuestIds.Add(__instance.Template.Id);

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MoreCheckmarksMod.LogError("Failed to process change in status for quest " + __instance.Template.Name +
                                           " to " + __instance.QuestStatus + ": " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }


    class ProfileSelectionPatch
    {
        // This postfix will run right after a profile has been selected
        static void Postfix()
        {
            MoreCheckmarksMod.modInstance.LoadData();
        }
    }

    /// <summary>
    /// Class to pass settings to the ConfigurationManager.
    /// Used by BepInEx.ConfigurationManager to control how settings appear in the F12 menu.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? ReadOnly;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string Category;
        public int? Order;
        public bool? Browsable;
        public string Description;
        public object DefaultValue;
        public bool? IsAdvanced;
    }
}
