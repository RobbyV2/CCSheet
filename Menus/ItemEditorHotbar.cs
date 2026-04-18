using CCSheet.CustomUI;
using CCSheet.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Globalization;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CCSheet.Menus
{
	internal class ItemEditorHotbar : UISlideWindow
	{
		internal static string CSText(string key, string category = "ItemEditor") => CCSheet.CSText(category, key);

		private CCSheet mod;
		private float spacing = 16f;
		public ItemEditorSlot slot;

		int stagedType, stagedStack, stagedPrefix, stagedDamage, stagedCrit, stagedUseTime, stagedUseAnimation, stagedShoot, stagedMana, stagedAmmo, stagedUseAmmo, stagedRare;
		float stagedKnockBack, stagedShootSpeed, stagedScale;
		bool stagedAutoReuse;

		private UITextbox tbType, tbStack, tbPrefix, tbDamage, tbKnockBack, tbCrit, tbUseTime, tbUseAnimation, tbShoot, tbShootSpeed, tbMana, tbScale, tbAmmo, tbUseAmmo, tbRare;
		private UIImage autoReuseToggle;

		public ItemEditorHotbar(CCSheet mod) {
			this.mod = mod;
			this.CanMove = true;
			base.Width = 360;
			base.Height = 380;

			Asset<Texture2D> closeTexture = CCSheet.instance.Assets.Request<Texture2D>("UI/closeButton", ReLogic.Content.AssetRequestMode.ImmediateLoad);
			UIImage closeBtn = new UIImage(closeTexture);
			closeBtn.Scale = 1.25f;
			closeBtn.Anchor = AnchorPosition.TopRight;
			closeBtn.Position = new Vector2(base.Width - spacing, spacing);
			closeBtn.onLeftClick += new EventHandler(this.bClose_onLeftClick);
			this.AddChild(closeBtn);

			UILabel title = new UILabel(CSText("ShowItemEditor"));
			title.font = FontAssets.MouseText.Value;
			title.Scale = 1f;
			title.Position = new Vector2(spacing, spacing);
			this.AddChild(title);

			slot = new ItemEditorSlot(this);
			slot.Position = new Vector2(spacing, 40);
			this.AddChild(slot);

			float col1 = 16, col2 = 190, widgetOffset = 110, rowStride = 28, startY = 110, tbWidth = 50;

			AddRow(0, col1, col2, startY + 0 * rowStride, widgetOffset, tbWidth, "Type", "Stack", out tbType, out tbStack);
			AddRow(1, col1, col2, startY + 1 * rowStride, widgetOffset, tbWidth, "Prefix", "Damage", out tbPrefix, out tbDamage);
			AddRow(2, col1, col2, startY + 2 * rowStride, widgetOffset, tbWidth, "KnockBack", "Crit", out tbKnockBack, out tbCrit);
			AddRow(3, col1, col2, startY + 3 * rowStride, widgetOffset, tbWidth, "UseTime", "UseAnimation", out tbUseTime, out tbUseAnimation);
			AddRow(4, col1, col2, startY + 4 * rowStride, widgetOffset, tbWidth, "Shoot", "ShootSpeed", out tbShoot, out tbShootSpeed);
			AddRow(5, col1, col2, startY + 5 * rowStride, widgetOffset, tbWidth, "Mana", "Scale", out tbMana, out tbScale);
			AddRow(6, col1, col2, startY + 6 * rowStride, widgetOffset, tbWidth, "Ammo", "UseAmmo", out tbAmmo, out tbUseAmmo);

			float row7Y = startY + 7 * rowStride;
			UILabel lblAuto = new UILabel(CSText("AutoReuse"));
			lblAuto.font = FontAssets.MouseText.Value;
			lblAuto.Scale = 1f;
			lblAuto.Position = new Vector2(col1, row7Y);
			this.AddChild(lblAuto);

			autoReuseToggle = new UIImage(TextureAssets.InventoryTickOff);
			autoReuseToggle.Position = new Vector2(col1 + widgetOffset, row7Y);
			autoReuseToggle.onLeftClick += (s, e) => {
				stagedAutoReuse = !stagedAutoReuse;
				autoReuseToggle.TextureAsset = stagedAutoReuse ? TextureAssets.InventoryTickOn : TextureAssets.InventoryTickOff;
			};
			this.AddChild(autoReuseToggle);

			UILabel lblRare = new UILabel(CSText("Rare"));
			lblRare.font = FontAssets.MouseText.Value;
			lblRare.Scale = 1f;
			lblRare.Position = new Vector2(col2, row7Y);
			this.AddChild(lblRare);

			tbRare = MakeTextbox(false, 10);
			tbRare.Position = new Vector2(col2 + widgetOffset, row7Y);
			tbRare.Width = tbWidth;
			this.AddChild(tbRare);

			tbKnockBack.HasDecimal = true;
			tbShootSpeed.HasDecimal = true;
			tbScale.HasDecimal = true;

			UITextButton applyBtn = new UITextButton(CSText("Apply"), 80, 32);
			applyBtn.Position = new Vector2(base.Width / 2 - 40, base.Height - 44);
			applyBtn.onLeftClick += (s, e) => Apply();
			this.AddChild(applyBtn);
		}

		public override void Draw(SpriteBatch spriteBatch) {
			base.Draw(spriteBatch);

			if (Visible && IsMouseInside()) {
				Main.LocalPlayer.mouseInterface = true;
				Main.LocalPlayer.cursorItemIconEnabled = false;
			}

			float x = FontAssets.MouseText.Value.MeasureString(UIView.HoverText).X;
			Vector2 vector = new Vector2((float)Main.mouseX, (float)Main.mouseY) + new Vector2(16f);
			if (vector.Y > (float)(Main.screenHeight - 30)) {
				vector.Y = (float)(Main.screenHeight - 30);
			}
			if (vector.X > (float)Main.screenWidth - x) {
				vector.X = (float)(Main.screenWidth - 460);
			}
			Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, UIView.HoverText, vector.X, vector.Y, new Color((int)Main.mouseTextColor, (int)Main.mouseTextColor, (int)Main.mouseTextColor, (int)Main.mouseTextColor), Color.Black, Vector2.Zero, 1f);
		}

		private void bClose_onLeftClick(object sender, EventArgs e) {
			Hide();
			mod.hotbar.DisableAllWindows();
		}

		private void AddRow(int row, float col1, float col2, float y, float widgetOffset, float tbWidth, string key1, string key2, out UITextbox a, out UITextbox b) {
			UILabel l1 = new UILabel(CSText(key1));
			l1.font = FontAssets.MouseText.Value;
			l1.Scale = 1f;
			l1.Position = new Vector2(col1, y);
			this.AddChild(l1);

			a = MakeTextbox(false, 10);
			a.Position = new Vector2(col1 + widgetOffset, y);
			a.Width = tbWidth;
			this.AddChild(a);

			UILabel l2 = new UILabel(CSText(key2));
			l2.font = FontAssets.MouseText.Value;
			l2.Scale = 1f;
			l2.Position = new Vector2(col2, y);
			this.AddChild(l2);

			b = MakeTextbox(false, 10);
			b.Position = new Vector2(col2 + widgetOffset, y);
			b.Width = tbWidth;
			this.AddChild(b);
		}

		private UITextbox MakeTextbox(bool hasDecimal, int maxChars) {
			UITextbox tb = new UITextbox();
			tb.Numeric = true;
			tb.HasDecimal = hasDecimal;
			tb.MaxCharacters = maxChars;
			return tb;
		}

		public void RepopulateFromItem(Item item) {
			stagedType = item.type;
			stagedStack = item.stack;
			stagedPrefix = item.prefix;
			stagedDamage = item.damage;
			stagedCrit = item.crit;
			stagedKnockBack = item.knockBack;
			stagedUseTime = item.useTime;
			stagedUseAnimation = item.useAnimation;
			stagedShoot = item.shoot;
			stagedShootSpeed = item.shootSpeed;
			stagedMana = item.mana;
			stagedScale = item.scale;
			stagedAmmo = item.ammo;
			stagedUseAmmo = item.useAmmo;
			stagedAutoReuse = item.autoReuse;
			stagedRare = item.rare;

			tbType.Text = stagedType.ToString();
			tbStack.Text = stagedStack.ToString();
			tbPrefix.Text = stagedPrefix.ToString();
			tbDamage.Text = stagedDamage.ToString();
			tbCrit.Text = stagedCrit.ToString();
			tbKnockBack.Text = stagedKnockBack.ToString(CultureInfo.InvariantCulture);
			tbUseTime.Text = stagedUseTime.ToString();
			tbUseAnimation.Text = stagedUseAnimation.ToString();
			tbShoot.Text = stagedShoot.ToString();
			tbShootSpeed.Text = stagedShootSpeed.ToString(CultureInfo.InvariantCulture);
			tbMana.Text = stagedMana.ToString();
			tbScale.Text = stagedScale.ToString(CultureInfo.InvariantCulture);
			tbAmmo.Text = stagedAmmo.ToString();
			tbUseAmmo.Text = stagedUseAmmo.ToString();
			tbRare.Text = stagedRare.ToString();
			autoReuseToggle.TextureAsset = stagedAutoReuse ? TextureAssets.InventoryTickOn : TextureAssets.InventoryTickOff;
		}

		public void Apply() {
			if (int.TryParse(tbType.Text, out int pType)) stagedType = pType;
			if (int.TryParse(tbStack.Text, out int pStack)) stagedStack = pStack;
			if (int.TryParse(tbPrefix.Text, out int pPrefix)) stagedPrefix = pPrefix;
			if (int.TryParse(tbDamage.Text, out int pDamage)) stagedDamage = pDamage;
			if (int.TryParse(tbCrit.Text, out int pCrit)) stagedCrit = pCrit;
			if (float.TryParse(tbKnockBack.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pKnockBack)) stagedKnockBack = pKnockBack;
			if (int.TryParse(tbUseTime.Text, out int pUseTime)) stagedUseTime = pUseTime;
			if (int.TryParse(tbUseAnimation.Text, out int pUseAnim)) stagedUseAnimation = pUseAnim;
			if (int.TryParse(tbShoot.Text, out int pShoot)) stagedShoot = pShoot;
			if (float.TryParse(tbShootSpeed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pShootSpeed)) stagedShootSpeed = pShootSpeed;
			if (int.TryParse(tbMana.Text, out int pMana)) stagedMana = pMana;
			if (float.TryParse(tbScale.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pScale)) stagedScale = pScale;
			if (int.TryParse(tbAmmo.Text, out int pAmmo)) stagedAmmo = pAmmo;
			if (int.TryParse(tbUseAmmo.Text, out int pUseAmmo)) stagedUseAmmo = pUseAmmo;
			if (int.TryParse(tbRare.Text, out int pRare)) stagedRare = pRare;

			if (stagedType < 0 || stagedType >= TextureAssets.Item.Length) {
				Main.NewText(CSText("InvalidType"), Color.Red);
				return;
			}

			slot.item.SetDefaults(stagedType);
			slot.item.stack = Math.Clamp(stagedStack, 1, slot.item.maxStack);
			slot.item.damage = stagedDamage;
			slot.item.crit = stagedCrit;
			slot.item.knockBack = stagedKnockBack;
			slot.item.useTime = stagedUseTime;
			slot.item.useAnimation = stagedUseAnimation;
			slot.item.shoot = stagedShoot;
			slot.item.shootSpeed = stagedShootSpeed;
			slot.item.mana = stagedMana;
			slot.item.scale = stagedScale;
			slot.item.ammo = stagedAmmo;
			slot.item.useAmmo = stagedUseAmmo;
			slot.item.autoReuse = stagedAutoReuse;
			slot.item.rare = stagedRare;
			slot.item.Prefix(stagedPrefix);
		}

		public void RepopulateEditor() {
			if (slot.item.type > 0) {
				RepopulateFromItem(slot.item);
			}
			else {
				ClearFields();
			}
		}

		public void ClearFields() {
			stagedType = stagedStack = stagedPrefix = stagedDamage = stagedCrit = stagedUseTime = stagedUseAnimation = stagedShoot = stagedMana = stagedAmmo = stagedUseAmmo = stagedRare = 0;
			stagedKnockBack = stagedShootSpeed = stagedScale = 0f;
			stagedAutoReuse = false;
			tbType.Text = tbStack.Text = tbPrefix.Text = tbDamage.Text = tbCrit.Text = tbKnockBack.Text = tbUseTime.Text = tbUseAnimation.Text = tbShoot.Text = tbShootSpeed.Text = tbMana.Text = tbScale.Text = tbAmmo.Text = tbUseAmmo.Text = tbRare.Text = "";
			autoReuseToggle.TextureAsset = TextureAssets.InventoryTickOff;
		}
	}

	internal class ItemEditorSlot : UIView
	{
		public Item item = new Item();
		private ItemEditorHotbar parent;

		public ItemEditorSlot(ItemEditorHotbar parent) {
			this.parent = parent;
			item.SetDefaults(0);
			base.onLeftClick += new EventHandler(this.Slot_onLeftClick);
			base.onHover += new EventHandler(this.Slot_onHover);
		}

		protected override float GetWidth() {
			return (float)GenericItemSlot.backgroundTexture.Width() * base.Scale;
		}

		protected override float GetHeight() {
			return (float)GenericItemSlot.backgroundTexture.Height() * base.Scale;
		}

		private void Slot_onHover(object sender, EventArgs e) {
			if (item.type > 0) {
				Main.hoverItemName = this.item.Name;
				Main.HoverItem = item.Clone();
				Main.HoverItem.SetNameOverride(Main.HoverItem.Name + (Main.HoverItem.ModItem != null ? " [" + Main.HoverItem.ModItem.Mod.Name + "]" : ""));
			}
		}

		private void Slot_onLeftClick(object sender, EventArgs e) {
			Player player = Main.LocalPlayer;
			if (player.itemAnimation == 0 && player.itemTime == 0) {
				Item held = Main.mouseItem.Clone();
				Main.mouseItem = this.item.Clone();
				if (Main.mouseItem.type > 0) {
					Main.playerInventory = true;
				}
				this.item = held.Clone();
				parent.RepopulateEditor();
			}
		}

		public override void Draw(SpriteBatch spriteBatch) {
			spriteBatch.Draw(RecipeQuerySlot.backgroundTexture.Value, base.DrawPosition, null, Color.White, 0f, Vector2.Zero, base.Scale, SpriteEffects.None, 0f);
			Texture2D texture2D = ModUtils.GetItemTexture(item.type).Value;
			Rectangle rectangle2;
			if (Main.itemAnimations[item.type] != null) {
				rectangle2 = Main.itemAnimations[item.type].GetFrame(texture2D);
			}
			else {
				rectangle2 = texture2D.Frame(1, 1, 0, 0);
			}
			float num = 1f;
			float num2 = (float)Slot.backgroundTexture.Width() * base.Scale * 0.6f;
			if ((float)rectangle2.Width > num2 || (float)rectangle2.Height > num2) {
				if (rectangle2.Width > rectangle2.Height) {
					num = num2 / (float)rectangle2.Width;
				}
				else {
					num = num2 / (float)rectangle2.Height;
				}
			}
			Vector2 drawPosition = base.DrawPosition;
			drawPosition.X += (float)Slot.backgroundTexture.Width() * base.Scale / 2f - (float)rectangle2.Width * num / 2f;
			drawPosition.Y += (float)Slot.backgroundTexture.Height() * base.Scale / 2f - (float)rectangle2.Height * num / 2f;
			this.item.GetColor(Color.White);
			spriteBatch.Draw(texture2D, drawPosition, new Rectangle?(rectangle2), this.item.GetAlpha(Color.White), 0f, Vector2.Zero, num, SpriteEffects.None, 0f);
			if (this.item.color != default(Color)) {
				spriteBatch.Draw(texture2D, drawPosition, new Rectangle?(rectangle2), this.item.GetColor(Color.White), 0f, Vector2.Zero, num, SpriteEffects.None, 0f);
			}
			if (this.item.stack > 1) {
				spriteBatch.DrawString(FontAssets.ItemStack.Value, this.item.stack.ToString(), new Vector2(base.DrawPosition.X + 10f * base.Scale, base.DrawPosition.Y + 26f * base.Scale), Color.White, 0f, Vector2.Zero, base.Scale, SpriteEffects.None, 0f);
			}
			base.Draw(spriteBatch);
		}
	}

	internal class UITextButton : UIView
	{
		private string text;
		private DynamicSpriteFont font;

		public UITextButton(string text, float width, float height) {
			this.text = text;
			this.font = FontAssets.MouseText.Value;
			base.Width = width;
			base.Height = height;
			base.BackgroundColor = new Color(63, 45, 121, 255) * 0.785f;
		}

		public override void Draw(SpriteBatch spriteBatch) {
			float x = base.DrawPosition.X - base.Origin.X;
			float y = base.DrawPosition.Y - base.Origin.Y;
			Utils.DrawInvBG(spriteBatch, x, y, base.Width, base.Height, base.BackgroundColor);
			Vector2 size = font.MeasureString(text);
			Utils.DrawBorderStringFourWay(spriteBatch, font, text, x + base.Width / 2f, y + base.Height / 2f + 4f, Color.White, Color.Black, size / 2f, 1f);
			base.Draw(spriteBatch);
		}
	}
}
