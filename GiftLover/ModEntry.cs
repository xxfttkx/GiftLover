using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Network;

namespace GiftLover
{
    /// <summary>模组入口点</summary>
    public class ModEntry : Mod
    {
        private bool isGMCMRegistered = false;
        public class ModConfig
        {
            public int distance { get; set; } = 15;
            public int dayGiftLimit { get; set; } = 1;
            public int weekGiftLimit { get; set; } = 2;
            public int xOffest { get; set; } = 32;
            public int yOffest { get; set; } = -64;
            public bool hideWhenHoldingTool { get; set; } = true;

            /// <summary>如果NPC好感度已满，是否隐藏图标</summary>
            public bool hideWhenFriendshipMaxed { get; set; } = true;
            /// <summary>如果没有送礼次数，是否隐藏图标</summary>
            public bool hideWhenGiftLimitReached { get; set; } = true;
        }
        private ModConfig modConfig;
        private Dictionary<string, Texture2D> tasteIcons = new();
        private Item currentItem;
        private List<NPC> nearbyNpcs = new();
        private Dictionary<NPC, Dictionary<Item, String>> npcAndItem = new();
        /*********
        ** 公共方法
        *********/
        /// <summary>模组的入口点，在首次加载模组后自动调用</summary>
        /// <param name="helper">对象 helper 提供用于编写模组的简化接口</param>
        public override void Entry(IModHelper helper)
        {
            this.modConfig = helper.ReadConfig<ModConfig>();
            helper.ConsoleCommands.Add("reload_config", "Reload the config file", ReloadConfig);
            // 加载图标资源
            tasteIcons = new()
            {
                ["love"] = helper.ModContent.Load<Texture2D>("assets/love.png"),
                ["like"] = helper.ModContent.Load<Texture2D>("assets/like.png"),
                ["dislike"] = helper.ModContent.Load<Texture2D>("assets/dislike.png"),
                ["hate"] = helper.ModContent.Load<Texture2D>("assets/hate.png"),
                ["neutral"] = helper.ModContent.Load<Texture2D>("assets/neutral.png"),
            };
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void ReloadConfig(string command, string[] args)
        {
            this.modConfig = Helper.ReadConfig<ModConfig>();
            Monitor.Log("配置已重新加载", LogLevel.Info);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            if (!isGMCMRegistered)
            {
                RegisterGMCM();
                isGMCMRegistered = true;
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            currentItem = Game1.player.CurrentItem;
        }

        public String GetNpcGiftTaste(NPC npc, Item item)
        {
            if (npc == null || item == null)
                return "null";
            // 获取 NPC 对物品的喜好值
            if (!npcAndItem.ContainsKey(npc))
            {
                npcAndItem[npc] = new Dictionary<Item, String>();
            }
            if (!npcAndItem[npc].ContainsKey(item))
            {
                npcAndItem[npc][item] = GetGiftReaction(npc.getGiftTasteForThisItem(item));
                this.Monitor.Log($"{npc.Name} {npcAndItem[npc][item]} {currentItem.Name}.", LogLevel.Trace);
            }
            return npcAndItem[npc][item];
        }
        public String GetGiftReaction(int index)
        {
            return index switch
            {
                0 or 1 => "love",
                2 or 3 => "like",
                4 or 5 => "dislike",
                6 or 7 => "hate",
                8 or 9 => "neutral",
                _ => "none"
            };
        }
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // 正在剧情动画中
            if (Game1.CurrentEvent != null)
                return;
            if (modConfig.hideWhenHoldingTool && Game1.player.CurrentTool != null)
                return;
            if (currentItem == null)
                return;
            nearbyNpcs.Clear();
            foreach (NPC npc in Game1.currentLocation.characters)
            {
                if (npc.IsVillager && npc.CanReceiveGifts() && npc.withinPlayerThreshold(modConfig.distance)) // 4 tile threshold
                {
                    nearbyNpcs.Add(npc);
                }
            }
            foreach (var npc in nearbyNpcs)
            {
                // 若开启了“满好感隐藏”并且NPC好感度已满，则跳过绘制
                if (modConfig.hideWhenFriendshipMaxed && IsMaxFriendship(npc))
                    continue;
                // 如果配置启用了“送礼次数用完时隐藏”，并且该 NPC 本日或本周的送礼次数已用尽，则不显示图标
                if (modConfig.hideWhenGiftLimitReached && IsGiftLimitReached(npc))
                    continue;

                // 判断 NPC 喜好
                var taste = GetNpcGiftTaste(npc, currentItem);

                Texture2D icon = GetIconForTaste(taste);
                if (icon != null)
                {
                    Vector2 worldPos = new Vector2(npc.Position.X + modConfig.xOffest, npc.Position.Y + modConfig.yOffest);
                    Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, worldPos);
                    e.SpriteBatch.Draw(icon, screenPos, null, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
                }
            }
        }

        private Texture2D GetIconForTaste(string taste)
        {
            if (tasteIcons.TryGetValue(taste.ToLower(), out Texture2D texture))
                return texture;
            return null;
        }

        private bool IsMaxFriendship(NPC npc)
        {
            if (npc == null || Game1.player.friendshipData == null)
                return false;

            Friendship friendship;
            if (!Game1.player.friendshipData.TryGetValue(npc.Name, out friendship))
                return false;

            int maxPoints = npc.isMarried() ? 3250 : 2500;
            return friendship.Points >= maxPoints;
        }

        private bool IsGiftLimitReached(NPC npc)
        {
            if (npc == null || Game1.player.friendshipData == null)
                return false;

            if (!Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship friendship))
                return false;

            // 每周最多 2 次礼物（不包括生日）
            if (friendship.GiftsThisWeek >= modConfig.weekGiftLimit && !npc.isBirthday())
                return true;

            // 今天是否已经送过
            if (friendship.GiftsToday >= modConfig.dayGiftLimit)
                return true;

            return false;
        }

        private void RegisterGMCM()
        {
            var gmcmApi = Helper.ModRegistry
                .GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi == null)
                return;

            gmcmApi.Register(
                mod: ModManifest,
                reset: () => modConfig = new ModConfig(),
                save: () => Helper.WriteConfig(modConfig)
            );

            gmcmApi.AddNumberOption(
                mod: ModManifest,
                name: () => "显示距离",
                tooltip: () => "玩家与 NPC 的最大距离，超出此距离图标将不显示",
                getValue: () => modConfig.distance,
                setValue: value => modConfig.distance = value,
                min: 1,
                max: 100
            );

            gmcmApi.AddNumberOption(
                mod: ModManifest,
                name: () => "每日送礼次数",
                tooltip: () => "每日允许送礼的最大次数",
                getValue: () => modConfig.dayGiftLimit,
                setValue: value => modConfig.dayGiftLimit = value,
                min: 0,
                max: 10
            );

            gmcmApi.AddNumberOption(
                mod: ModManifest,
                name: () => "每周送礼次数",
                tooltip: () => "每周允许送礼的最大次数",
                getValue: () => modConfig.weekGiftLimit,
                setValue: value => modConfig.weekGiftLimit = value,
                min: 0,
                max: 99
            ); 
            gmcmApi.AddNumberOption(
                mod: ModManifest,
                name: () => "xOffest",
                tooltip: () => "xOffest",
                getValue: () => modConfig.xOffest,
                setValue: value => modConfig.xOffest = value,
                min: -100,
                max: 100
            );
            gmcmApi.AddNumberOption(
                mod: ModManifest,
                name: () => "yOffest",
                tooltip: () => "yOffest",
                getValue: () => modConfig.yOffest,
                setValue: value => modConfig.yOffest = value,
                min: -100,
                max: 100
            );

            gmcmApi.AddBoolOption(
                mod: ModManifest,
                name: () => "拿工具时隐藏图标",
                tooltip: () => "当你手持工具（如锄头、斧头）时，不显示图标",
                getValue: () => modConfig.hideWhenHoldingTool,
                setValue: value => modConfig.hideWhenHoldingTool = value
            );

            gmcmApi.AddBoolOption(
                mod: ModManifest,
                name: () => "好感已满时隐藏图标",
                tooltip: () => "NPC 好感度已满时不显示图标",
                getValue: () => modConfig.hideWhenFriendshipMaxed,
                setValue: value => modConfig.hideWhenFriendshipMaxed = value
            );

            gmcmApi.AddBoolOption(
                mod: ModManifest,
                name: () => "送礼次数用尽时隐藏图标",
                tooltip: () => "若无法继续送礼（每日/每周次数用尽），不显示图标",
                getValue: () => modConfig.hideWhenGiftLimitReached,
                setValue: value => modConfig.hideWhenGiftLimitReached = value
            );
        }
    }
}