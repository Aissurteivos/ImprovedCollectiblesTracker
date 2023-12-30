using BepInEx;
using BepInEx.Logging;
using System.Security.Permissions;
using System.Security;
using System;
using System.Collections.Generic;
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
    [BepInPlugin(AUTHOR + "." + MOD_ID, MOD_NAME, VERSION)]
    internal class Plugin : BaseUnityPlugin {
        public static new ManualLogSource Logger { get; private set; } = null!;

        public const string VERSION = "1.0.0";
        public const string MOD_NAME = "ImprovedCollectiblesTracker";
        public const string MOD_ID = "improvedcollectiblestracker";
        public const string AUTHOR = "aissurtievos";

        public void OnEnable() {
            Logger = base.Logger;
            On.MoreSlugcats.CollectiblesTracker.ctor += CollectiblesTracker_ctor;
            On.MoreSlugcats.CollectiblesTracker.GrafUpdate += CollectiblesTracker_GrafUpdate;
        }

        public bool IsMouseWithin(Rect rect) {
            return Futile.mousePosition.x > rect.x &&
                   Futile.mousePosition.y > rect.y &&
                   Futile.mousePosition.x < rect.x + rect.width &&
                   Futile.mousePosition.y < rect.y + rect.height;
        }

        public static FLabel? ToolTipText = null;
        public static RoundedRect? TooltipBoundingBox = null;
        public static string LastTooltipValue = "";
        public static int VisibilityCounter = 0;

        private void CollectiblesTracker_GrafUpdate(On.MoreSlugcats.CollectiblesTracker.orig_GrafUpdate orig, CollectiblesTracker s, float timeStacker) {
            try {
                orig(s, timeStacker);

                // Base our rearrangement on the original position of the top right icon
                var topRightNode = s.regionIcons.Last();
                var startX = topRightNode.x + 26f;
                var startY = topRightNode.y;
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
                }

                // These should never be null, but check anyway
                if (ToolTipText == null || TooltipBoundingBox == null) { return; }

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
                    if (VisibilityCounter == 0) { return; }
                    VisibilityCounter = 0;
                    LastTooltipValue = "";
                    ToolTipText.isVisible = false;
                    TooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.isVisible = false);
                    return;
                } else if (VisibilityCounter == 0) {
                    ToolTipText.isVisible = true;
                    TooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.isVisible = true);
                }

                var fullRegionName = Region.GetRegionFullName(hoveredLabel.text.ToLowerInvariant(), CurrentSlugcat);

                if (fullRegionName != LastTooltipValue) {
                    ToolTipText.text = fullRegionName;
                    LastTooltipValue = fullRegionName;
                } else if (VisibilityCounter < 20) {
                    VisibilityCounter += 1;
                }

                var fullNameRect = ToolTipText.textRect;
                var hPadding = 12f;
                var vPadding = 6f;

                TooltipBoundingBox.pos.x = Mathf.Floor(Futile.mousePosition.x - fullNameRect.width - hPadding - 13f);
                TooltipBoundingBox.pos.y = Mathf.Floor(Futile.mousePosition.y - fullNameRect.height / 2);

                TooltipBoundingBox.size.x = fullNameRect.width + hPadding;
                TooltipBoundingBox.size.y = fullNameRect.height + vPadding;

                var boundingBoxDrawPos = TooltipBoundingBox.DrawPos(timeStacker);

                ToolTipText.x = boundingBoxDrawPos.x + fullNameRect.width / 2 + 0.01f + hPadding / 2f;
                ToolTipText.y = boundingBoxDrawPos.y + fullNameRect.height / 2 + 0.01f + vPadding / 2f + 1f;


                // elements should be invisible for the first 5 frames, and fade in gradually after that over 15 frames
                // final opacity 75%
                var elementAlpha = Max(0f, (VisibilityCounter - 5) * 0.05f);

                ToolTipText.alpha = elementAlpha;
                TooltipBoundingBox.fillAlpha = elementAlpha;
                TooltipBoundingBox.sprites.ToList().ForEach(sprite => sprite.alpha = elementAlpha);
            } catch (Exception e) { Logger.LogError($"Exception in CollectiblesTracker_GrafUpdate {e}"); }
        }

        public static List<FLabel> RegionLabels = [];
        public static FLabel? RegionsLeftText = null;
        public static SlugcatStats.Name CurrentSlugcat = new("White", register: true);
        // Replace original constructor with more efficient one, also with custom logic as well
        private void CollectiblesTracker_ctor(On.MoreSlugcats.CollectiblesTracker.orig_ctor orig, CollectiblesTracker s, Menu.Menu m, MenuObject owner, Vector2 pos, FContainer container, SlugcatStats.Name saveSlot) {
            try {
                orig(s, m, owner, pos, container, saveSlot);
                s.sprites
                    .ToList()
                    .ForEach(region => region.Value
                        .ForEach(sprite => {
                            container.RemoveChild(sprite);
                        })
                    );
                s.sprites.Clear();

                var menu = m as SleepAndDeathScreen;
                var rainWorld = menu.manager.rainWorld;
                CurrentSlugcat = saveSlot;

                var availableRegions = SlugcatStats
                    .getSlugcatStoryRegions(saveSlot)
                    .Concat(SlugcatStats
                        .getSlugcatOptionalRegions(saveSlot))
                    .Select(region => region.ToLowerInvariant())
                    .ToList();

                s.displayRegions = availableRegions
                    .Where(s.collectionData.regionsVisited.Contains)
                    .ToList();

                s.regionIcons = s.displayRegions
                    .Select(region => {
                        var color = Color.Lerp(Region.RegionColor(region.ToUpperInvariant()), Color.white, 0.25f);
                        var sprite = region == s.collectionData.currentRegion ?
                            new FSprite("keyShiftB") { rotation = 270f, scale = 0.5f, color = color } : // Current region
                            new FSprite("Circle4") { color = color }; // All others
                        container.AddChild(sprite);
                        return sprite;
                    })
                    .ToArray();

                RegionLabels = s.displayRegions
                    .Select(region => {
                        var regionLabel = new FLabel(GetFont(), region.ToUpperInvariant()) { alignment = FLabelAlignment.Left };
                        container.AddChild(regionLabel);
                        return regionLabel;
                    })
                    .ToList();

                var regionsLeftToFind = availableRegions.Count() - s.displayRegions.Count();

                RegionsLeftText = regionsLeftToFind > 0 ? new FLabel(GetFont(), $"{regionsLeftToFind} regions left to discover!") { color = new Color(0.75f, 0.75f, 0.75f) } : null;
                if (RegionsLeftText != null)
                    container.AddChild(RegionsLeftText);

                foreach (var region in s.displayRegions) {
                    s.sprites[region] = [];
                    rainWorld.regionGoldTokens[region]
                        .Where((token, j) => rainWorld.regionGoldTokensAccessibility[region][j].Contains(saveSlot))
                        .ToList()
                        .ForEach(token => {
                            s.sprites[region].Add(new FSprite(s.collectionData.unlockedGolds.Contains(token) ? "ctOn" : "ctOff") { color = new Color(1f, 0.6f, 0.05f) });
                        });
                    rainWorld.regionBlueTokens[region]
                        .Where((token, j) => rainWorld.regionBlueTokensAccessibility[region][j].Contains(saveSlot))
                        .ToList()
                        .ForEach(token => {
                            s.sprites[region].Add(new FSprite(s.collectionData.unlockedBlues.Contains(token) ? "ctOn" : "ctOff") { color = RainWorld.AntiGold.rgb });
                        });

                    if (ModManager.MSC) {
                        rainWorld.regionGreenTokens[region]
                            .Where((token, j) => rainWorld.regionGreenTokensAccessibility[region][j].Contains(saveSlot))
                            .ToList()
                            .ForEach(token => {
                                s.sprites[region].Add(new FSprite(s.collectionData.unlockedGreens.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.GreenColor.rgb });
                            });
                        if (saveSlot == MoreSlugcatsEnums.SlugcatStatsName.Spear) {
                            rainWorld.regionGreyTokens[region]
                                .ForEach(token => {
                                    s.sprites[region].Add(new FSprite(s.collectionData.unlockedGreys.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.WhiteColor.rgb });
                                });
                        }
                        rainWorld.regionRedTokens[region]
                            .Where((token, j) => rainWorld.regionRedTokensAccessibility[region][j].Contains(saveSlot))
                            .ToList()
                            .ForEach(token => {
                                s.sprites[region].Add(new FSprite(s.collectionData.unlockedReds.Contains(token) ? "ctOn" : "ctOff") { color = CollectToken.RedColor.rgb });
                            });
                    }

                    if (!s.sprites[region].Any()) {
                        s.sprites[region].Add(new FSprite("ctNone") { color = CollectToken.WhiteColor.rgb });
                    }
                    s.sprites[region].ForEach(container.AddChild);
                }

                TooltipBoundingBox = new(menu, menu.pages[0], Vector2.zero, Vector2.one, true) {
                    borderColor = new HSLColor(0f, 0f, 0.25f),
                    fillAlpha = 0f
                };

                menu.pages[0].subObjects.Add(TooltipBoundingBox);

                ToolTipText = new(GetFont(), "");
                container.AddChild(ToolTipText);

            } catch (Exception e) {
                Logger.LogError($"Exception in CollectiblesTracker_ctor override {e}");
            }
        }
    }
}
