using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace GiftLover
{
    /// <summary>模组入口点</summary>
    public class ModEntry : Mod
    {
        public class ModConfig
        {
            public int distance { get; set; } = 15;

            /// <summary>如果NPC好感度已满，是否隐藏图标</summary>
            public bool hideWhenFriendshipMaxed { get; set; } = true;

            /// <summary>如果没有送礼次数，是否隐藏图标</summary>
            public bool hideWhenNoGiftLeft { get; set; } = true;
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
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void ReloadConfig(string command, string[] args)
        {
            this.modConfig = Helper.ReadConfig<ModConfig>();
            Monitor.Log("配置已重新加载", LogLevel.Info);
        }


        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            currentItem = Game1.player.CurrentItem;
            nearbyNpcs.Clear();

            foreach (NPC npc in Game1.currentLocation.characters)
            {
                if (npc.IsVillager && npc.withinPlayerThreshold(modConfig.distance)) // 4 tile threshold
                {
                    nearbyNpcs.Add(npc);
                }
            }
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
                this.Monitor.Log($"{npc.Name} {npcAndItem[npc][item]} {currentItem.Name}.", LogLevel.Debug);
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
            if (currentItem == null)
                return;

            foreach (var npc in nearbyNpcs)
            {
                // 判断 NPC 喜好
                var taste = GetNpcGiftTaste(npc, currentItem);
                
                Texture2D icon = GetIconForTaste(taste);
                if (icon != null)
                {
                    Vector2 worldPos = new Vector2(npc.Position.X + 30, npc.Position.Y - 64);
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
    }
}