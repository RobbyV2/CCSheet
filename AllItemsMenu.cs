using CCSheet.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace CCSheet
{
	internal class AllItemsMenu : GlobalItem
	{
		internal static Item[] singleSlotArray;

		public AllItemsMenu() {
			singleSlotArray = new Item[1];
		}

		//internal void UpdateInput()
		//{
		//	try
		//	{
		//		UIView.UpdateUpdateInput();
		//		CCSheet.instance.npcBrowser.Update();
		//		CCSheet.instance.itemBrowser.Update();
		//		CCSheet.instance.recipeBrowser.Update();
		//		CCSheet.instance.extendedCheatMenu.Update();

		//		CCSheet.instance.hotbar.Update();
		//		CCSheet.instance.paintToolsHotbar.Update();
		//		CCSheet.instance.quickTeleportHotbar.Update();
		//		CCSheet.instance.quickClearHotbar.Update();
		//		CCSheet.instance.npcButchererHotbar.Update();
		//		ConfigurationTool.configurationWindow.Update();
		//	}
		//	catch (Exception e)
		//	{
		//		ErrorLogger.Log(e.Message + " " + e.StackTrace);
		//	}
		//}

		public void DrawUpdateAll(SpriteBatch spriteBatch) {
			CCSheet.instance.itemBrowser.Draw(spriteBatch);
			CCSheet.instance.itemEditorHotbar.Draw(spriteBatch);
			CCSheet.instance.npcBrowser.Draw(spriteBatch);
			CCSheet.instance.recipeBrowser.Draw(spriteBatch);
			CCSheet.instance.extendedCheatMenu.Draw(spriteBatch);
			CCSheet.instance.paintToolsUI.Draw(spriteBatch);

			//			CCSheet.instance.itemBrowser.Update();
			//	spriteBatch.End();
			//	spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

			CCSheet.instance.npcBrowser.Update();
			CCSheet.instance.itemBrowser.Update();
			CCSheet.instance.itemEditorHotbar.Update();
			CCSheet.instance.recipeBrowser.Update();
			CCSheet.instance.extendedCheatMenu.Update();

			CCSheet.instance.hotbar.Update();
			CCSheet.instance.paintToolsHotbar.Update();
			CCSheet.instance.paintToolsUI.Update();
			CCSheet.instance.quickTeleportHotbar.Update();
			CCSheet.instance.quickClearHotbar.Update();
			CCSheet.instance.npcButchererHotbar.Update();
			ConfigurationTool.configurationWindow.Update();
			//BossDowner.bossDownerWindow.Update();
			//CCSheet.instance.eventManagerHotbar.Update();

			CCSheet.instance.hotbar.Draw(spriteBatch);
			CCSheet.instance.paintToolsHotbar.Draw(spriteBatch);
			CCSheet.instance.quickTeleportHotbar.Draw(spriteBatch);
			CCSheet.instance.quickClearHotbar.Draw(spriteBatch);
			CCSheet.instance.npcButchererHotbar.Draw(spriteBatch);
			ConfigurationTool.configurationWindow.Draw(spriteBatch);
			//BossDowner.bossDownerWindow.Draw(spriteBatch);
			//CCSheet.instance.eventManagerHotbar.Draw(spriteBatch);

			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

			//	DrawUpdateExtraAccessories(spriteBatch);
		}

		public void DrawUpdatePaintTools(SpriteBatch spriteBatch) {
			CCSheet.instance.paintToolsHotbar.UpdateGameScale();
			CCSheet.instance.paintToolsHotbar.DrawGameScale(spriteBatch);
		}

		internal void DrawUpdateExtraAccessories(SpriteBatch spriteBatch) {
			if (Main.playerInventory && Main.EquipPage == 0) {
				Point value = new Point(Main.mouseX, Main.mouseY);
				Rectangle r = new Rectangle(0, 0, (int)((float)TextureAssets.InventoryBack.Value.Width * Main.inventoryScale), (int)((float)TextureAssets.InventoryBack.Value.Height * Main.inventoryScale));

				CCSheetPlayer csp = Main.LocalPlayer.GetModPlayer<CCSheetPlayer>();
				for (int i = 0; i < csp.numberExtraAccessoriesEnabled; i++) {
					Main.inventoryScale = 0.85f;
					Item accItem = csp.ExtraAccessories[i];
					//if (accItem.type > 0)
					//{
					//	ErrorLogger.Log("aaa " + i + " " + accItem.type);
					//}

					int mH = 0;
					if (Main.mapEnabled) {
						if (!Main.mapFullscreen && Main.mapStyle == 1) {
							mH = 256;
						}
						if (mH + 600 > Main.screenHeight) {
							mH = Main.screenHeight - 600;
						}
					}

					int num17 = Main.screenWidth - 92 - (47 * 3);
					int num18 = /*Main.mH +*/mH + 174;
					if (Main.netMode == 1)
						num17 -= 47;
					r.X = num17 - (i / 10) * 47;
					r.Y = num18 + (i % 10) * 47;

					if (r.Contains(value)/* && !flag2*/) {
						Main.LocalPlayer.mouseInterface = true;
						Main.armorHide = true;
						singleSlotArray[0] = accItem;
						ItemSlot.Handle(singleSlotArray, ItemSlot.Context.EquipAccessory, 0);
						accItem = singleSlotArray[0];
						//ItemSlot.Handle(ref accItem, ItemSlot.Context.EquipAccessory);
					}
					singleSlotArray[0] = accItem;
					ItemSlot.Draw(spriteBatch, singleSlotArray, 10, 0, new Vector2(r.X, r.Y));
					accItem = singleSlotArray[0];

					//ItemSlot.Draw(spriteBatch, ref accItem, 10, new Vector2(r.X, r.Y));

					csp.ExtraAccessories[i] = accItem;
					//	ErrorLogger.Log("pd");
					//player.VanillaUpdateAccessory(csp.ExtraAccessories[i], false, ref wallSpeedBuff, ref tileSpeedBuff, ref tileRangeBuff);
				}
			}
		}
	}
}