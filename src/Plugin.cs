﻿using BepInEx;
using BepInEx.Logging;
using System.Security.Permissions;
using System.Security;
using System;
using System.Collections.Generic;
using System.Data;
using static System.Math;
using System.Linq;
using UnityEngine;
using static RWCustom.Custom;
using MoreSlugcats;
using Menu;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]
#pragma warning restore CS0618 // Type or member is obsolete

namespace ImprovedCollectiblesTracker {
    [BepInPlugin(Author + "." + ModID, ModName, Version)]
    public class Plugin : BaseUnityPlugin {
        private new static ManualLogSource Logger { get; set; } = null!;

        private const string Version = "1.3.0";
        private const string ModName = "ImprovedCollectiblesTracker";
        private const string ModID = "improvedcollectiblestracker";
        private const string Author = "aissurtievos";

        public void OnEnable() {
            Logger = base.Logger;
            On.MoreSlugcats.CollectiblesTracker.ctor += CollectiblesTracker_ctor;
            On.MoreSlugcats.CollectiblesTracker.GrafUpdate += CollectiblesTracker_GrafUpdate;
            On.MoreSlugcats.CollectiblesTracker.RemoveSprites += CollectiblesTracker_RemoveSprites;
            //On.RainWorld.PostModsInit += InitReferences;
        }

        // private static void InitReferences(On.RainWorld.orig_PostModsInit orig, RainWorld self) {
       //     SandboxUnlockIcons = rainWorld.regionBlueTokens.Values
       //         .SelectMany(tokenList => tokenList)
       //         .Distinct()
       //         .ToDictionary(token => token.value, MultiplayerUnlocks.SymbolDataForSandboxUnlock);
       // }

        private static bool IsMouseWithin(Rect rect) {
            return Futile.mousePosition.x > rect.x &&
                   Futile.mousePosition.y > rect.y &&
                   Futile.mousePosition.x < rect.x + rect.width &&
                   Futile.mousePosition.y < rect.y + rect.height;
        }

        private static FLabel? s_toolTipText = null;
        private static RoundedRect? s_tooltipBoundingBox = null;
        private static string s_lastTooltipValue = "";
        private static int s_visibilityCounter = 0;
        
        //private static Dictionary<string, IconSymbol.IconSymbolData> SandboxUnlockIcons = [];
        //private static Dictionary<FSprite, string> SpriteToUnlockID = [];

        private static void CollectiblesTracker_GrafUpdate(On.MoreSlugcats.CollectiblesTracker.orig_GrafUpdate orig, CollectiblesTracker s, float timeStacker) {
            try {
                orig(s, timeStacker);

                if (!RegionLabels.Any())
                    return;
                
                var drawpos = s.DrawPos(timeStacker);
                var startX = drawpos.x;
                var startY = drawpos.y;
                var minX = startX;
                var minY = startY;

                // Get the length of the widest label.
                var maxLength = RegionLabels.Max(label => label.textRect.width);

                // Rearrange the collectible labels, and add the labels for each one
                // layout is [collectibles listed right to left][two letter region code][region icon (dot or arrow)]
                for (var i = 0; i < s.displayRegions.Count; i++) {
                    minY = startY - i * 13f;
                    for (var j = 0; j < s.sprites[s.displayRegions[i]].Count; j++) {
                        minX = Min(startX - maxLength - (j + 1) * 13f, minX);
                        s.sprites[s.displayRegions[i]][j].x = startX - maxLength - (j + 1) * 13f;
                        s.sprites[s.displayRegions[i]][j].y = minY;
                    }
                    s.regionIcons[i].x = startX + 4f;
                    s.regionIcons[i].y = minY;
                    RegionLabels[i].x = startX - maxLength - 4f + 0.01f;
                    RegionLabels[i].y = minY + 0.01f;
                }

                // "XX regions left to discover!" text
                if (RegionsLeftText != null) {
                    var collectiblesWidth = startX + 4f - minX - 7f;
                    RegionsLeftText.x = startX - collectiblesWidth / 2;
                    RegionsLeftText.y = minY - 13f;
                    RegionsLeftText.isVisible = true;
                }

                // These should never be null, but check anyway
                if (s_toolTipText == null || s_tooltipBoundingBox == null) { return; }

                // Check if we are hovering over a label
                var hoveredLabel = RegionLabels
                    .FirstOrDefault(label => {
                        var labelRect = label.textRect;
                        labelRect.x = label.x;
                        labelRect.y = label.y - labelRect.height / 2;
                        return IsMouseWithin(labelRect);
                    });

                // If no label is being hovered over, instantly hide the tooltip and reset visibility
                if (hoveredLabel == null) {
                    if (s_visibilityCounter == 0) { return; }
                    s_visibilityCounter = 0;
                    s_lastTooltipValue = "";
                    s_toolTipText.isVisible = false;
                    s_tooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.isVisible = false);
                    return;
                }
                if (s_visibilityCounter == 0) {
                    s_toolTipText.isVisible = true;
                    s_tooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.isVisible = true);
                }

                var fullRegionName = Region.GetRegionFullName(hoveredLabel.text.ToLowerInvariant(), CurrentSlugcat);

                if (fullRegionName != s_lastTooltipValue) {
                    s_toolTipText.text = fullRegionName;
                    s_lastTooltipValue = fullRegionName;
                } else if (s_visibilityCounter < 20) {
                    s_visibilityCounter += 1;
                }

                RenderTooltip(timeStacker, s_toolTipText.textRect, 6f, 12f);
            } catch (Exception e) { Logger.LogError($"Exception in CollectiblesTracker_GrafUpdate {e}"); }
        }

        public static void RenderTooltip(float timeStacker, Rect fullNameRect, float hPadding, float vPadding)
        {
            s_tooltipBoundingBox.pos.x = Mathf.Floor(Futile.mousePosition.x - fullNameRect.width - hPadding - 13f);
            s_tooltipBoundingBox.pos.y = Mathf.Floor(Futile.mousePosition.y - fullNameRect.height / 2);

            s_tooltipBoundingBox.size.x = fullNameRect.width + hPadding;
            s_tooltipBoundingBox.size.y = fullNameRect.height + vPadding;

            var boundingBoxDrawPos = s_tooltipBoundingBox.DrawPos(timeStacker);

            s_toolTipText.x = boundingBoxDrawPos.x + fullNameRect.width / 2 + 0.01f + hPadding / 2f;
            s_toolTipText.y = boundingBoxDrawPos.y + fullNameRect.height / 2 + 0.01f + vPadding / 2f + 1f;


            // elements should be invisible for the first 5 frames, and fade in gradually after that over 15 frames
            // final opacity 75%
            var elementAlpha = Max(0f, (s_visibilityCounter - 5) * 0.05f);

            s_toolTipText.alpha = elementAlpha;
            s_tooltipBoundingBox.fillAlpha = elementAlpha;
            s_tooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.alpha = elementAlpha);
        }

        public static List<FLabel> RegionLabels = [];
        public static FLabel? RegionsLeftText = null;
        public static SlugcatStats.Name CurrentSlugcat = new("White", register: true);
        // Replace original constructor with more efficient one, also with custom logic as well
        private void CollectiblesTracker_ctor(On.MoreSlugcats.CollectiblesTracker.orig_ctor orig, CollectiblesTracker tracker, Menu.Menu menu, MenuObject owner, Vector2 pos, FContainer container, SlugcatStats.Name saveSlot) {
            try {
                orig(tracker, menu, owner, pos, container, saveSlot);
                
                RemoveExistingMenu(tracker, container);

                var rainWorld = menu.manager.rainWorld;
                CurrentSlugcat = saveSlot;

                var availableRegions = GenerateAvailableRegions(saveSlot);

                tracker.displayRegions = GenerateDisplayRegions(tracker, availableRegions);

                tracker.regionIcons = GenerateRegionIcons(tracker, container);

                RegionLabels = GenerateRegionLabels(tracker, container);

                var regionsLeftToFind = RegionsLeftToFind(tracker, availableRegions);

                RegionsLeftText = GenerateRegionsLeftText(regionsLeftToFind);
                
                if (RegionsLeftText != null)
                    container.AddChild(RegionsLeftText);
                
                foreach (var region in tracker.displayRegions) {
                    GenerateRegionTokens(tracker, container, saveSlot, region, rainWorld);
                }

                HandleTooltip(menu, container);
                
            } catch (Exception e) {
                Logger.LogError($"Exception in CollectiblesTracker_ctor override {e}");
            }
        }

        public static void GenerateRegionTokens(CollectiblesTracker tracker, FContainer container, SlugcatStats.Name saveSlot, string region, RainWorld rainWorld) {
            tracker.sprites[region] = [];
                        
            PopulateGoldTokens(tracker, saveSlot, rainWorld, region);
            PopulateBlueTokens(tracker, saveSlot, rainWorld, region);

            if (ModManager.MSC) {
                PopulateGreenTokens(tracker, saveSlot, rainWorld, region);
                PopulateGreyTokens(tracker, saveSlot, rainWorld, region);
                PopulateRedTokens(tracker, saveSlot, rainWorld, region);
            }

            if (!tracker.sprites[region].Any()) {
                tracker.sprites[region].Add(new FSprite("ctNone") { color = CollectToken.WhiteColor.rgb });
            }
            tracker.sprites[region].ForEach(container.AddChild);
        }

        public static void RemoveExistingMenu(CollectiblesTracker tracker, FContainer container) {
            tracker.sprites
                .ToList()
                .ForEach(region => region.Value
                    .ForEach(container.RemoveChild)
                );
            tracker.sprites.Clear();
        }

        public static List<string> GenerateAvailableRegions(SlugcatStats.Name saveSlot) {
            var regions = SlugcatStats
                .SlugcatStoryRegions(saveSlot)
                .Concat(SlugcatStats.SlugcatOptionalRegions(saveSlot))
                .Select(region => region.ToLowerInvariant())
                .ToList();
            return regions;
        }

        public static void HandleTooltip(Menu.Menu menu, FContainer container) {
            s_tooltipBoundingBox = new(menu, menu.pages[0], Vector2.zero, Vector2.one, true) {
                borderColor = new HSLColor(0f, 0f, 0.25f),
                fillAlpha = 0f
            };

            menu.pages[0].subObjects.Add(s_tooltipBoundingBox);

            s_toolTipText = new(GetFont(), "");
            container.AddChild(s_toolTipText);
        }

        public static void PopulateRedTokens(CollectiblesTracker tracker, SlugcatStats.Name saveSlot, RainWorld rainWorld, string region) {
            rainWorld.regionRedTokens[region]
                .Where((token, j) => rainWorld.regionRedTokensAccessibility[region][j].Contains(saveSlot))
                .ToList()
                .ForEach(token => {
                    tracker.sprites[region].Add(new FSprite(tracker.collectionData.unlockedReds.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.RedColor.rgb });
                });
        }

        public static void PopulateGreyTokens(CollectiblesTracker tracker, SlugcatStats.Name saveSlot, RainWorld rainWorld, string region) {
            if (saveSlot == MoreSlugcatsEnums.SlugcatStatsName.Spear) {
                rainWorld.regionGreyTokens[region]
                    .ForEach(token => {
                        tracker.sprites[region].Add(new FSprite(tracker.collectionData.unlockedGreys.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.WhiteColor.rgb });
                    });
            }
        }

        public static void PopulateGreenTokens(CollectiblesTracker tracker, SlugcatStats.Name saveSlot, RainWorld rainWorld, string region) {
            rainWorld.regionGreenTokens[region]
                .Where((token, j) => rainWorld.regionGreenTokensAccessibility[region][j].Contains(saveSlot))
                .ToList()
                .ForEach(token => {
                    tracker.sprites[region].Add(new FSprite(tracker.collectionData.unlockedGreens.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.GreenColor.rgb });
                });
        }

        public static void PopulateBlueTokens(CollectiblesTracker tracker, SlugcatStats.Name saveSlot, RainWorld rainWorld, string region) {
            rainWorld.regionBlueTokens[region]
                .Where((token, j) => rainWorld.regionBlueTokensAccessibility[region][j].Contains(saveSlot))
                .ToList()
                .ForEach(token => {
                    var sprite =
                        new FSprite(tracker.collectionData.unlockedBlues.Contains(token) ? "ctOn" : "ctOff") {
                            color = RainWorld.AntiGold.rgb
                        };
                    //SpriteToUnlockID.Add(sprite, token.value);
                    tracker.sprites[region].Add(sprite);
                });
        }

        public static void PopulateGoldTokens(CollectiblesTracker tracker, SlugcatStats.Name saveSlot, RainWorld rainWorld, string region) {
            rainWorld.regionGoldTokens[region]
                .Where((token, j) => rainWorld.regionGoldTokensAccessibility[region][j].Contains(saveSlot))
                .ToList()
                .ForEach(token => {
                    tracker.sprites[region].Add(new FSprite(tracker.collectionData.unlockedGolds.Contains(token) ? "ctOn" : "ctOff") { color = new Color(1f, 0.6f, 0.05f) });
                });
        }

        public static FLabel? GenerateRegionsLeftText(int regionsLeftToFind) {
            return regionsLeftToFind > 0 ? new FLabel(GetFont(), $"{regionsLeftToFind} regions left to discover!") { color = new Color(0.75f, 0.75f, 0.75f), isVisible = false } : null;
        }

        public static int RegionsLeftToFind(CollectiblesTracker tracker, List<string> availableRegions) {
            return availableRegions.Count() - tracker.displayRegions.Count();
        }

        public static List<FLabel> GenerateRegionLabels(CollectiblesTracker tracker, FContainer container) {
            var labels = tracker.displayRegions
                .Select(region => {
                    var regionLabel = new FLabel(GetFont(), region.ToUpperInvariant()) { alignment = FLabelAlignment.Left };
                    container.AddChild(regionLabel);
                    return regionLabel;
                })
                .ToList();
            return labels;
        }

        public static FSprite[] GenerateRegionIcons(CollectiblesTracker tracker, FContainer container) {
            var icons = tracker.displayRegions
                .Select(region => {
                    var color = Color.Lerp(Region.RegionColor(region.ToUpperInvariant()), Color.white, 0.25f);
                    var sprite = region == tracker.collectionData.currentRegion ?
                        new FSprite("keyShiftB") { rotation = 270f, scale = 0.5f, color = color } : // Current region
                        new FSprite("Circle4") { color = color }; // All others
                    container.AddChild(sprite);
                    return sprite;
                })
                .ToArray();
            return icons;
        }

        public static List<string> GenerateDisplayRegions(CollectiblesTracker tracker, List<string> availableRegions) {
            var regions = availableRegions
                .Where(tracker.collectionData.regionsVisited.Contains)
                .ToList();
            return regions;
        }

        private void CollectiblesTracker_RemoveSprites(On.MoreSlugcats.CollectiblesTracker.orig_RemoveSprites orig, CollectiblesTracker self) {
            orig(self);
            foreach (var label in RegionLabels)
            {
                label.RemoveFromContainer();
            }
            RegionsLeftText?.RemoveFromContainer();
            RegionLabels.Clear();
            RegionsLeftText = null;
        }
    }
}
