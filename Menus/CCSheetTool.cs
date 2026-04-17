using CCSheet.UI;
using Terraria.ModLoader;

namespace CCSheet.Menus
{
	abstract class CCSheetTool
	{
		public CCSheetTool(Mod mod) {

		}

		public static UIImage hotbarButton;

		public abstract UIImage GetButton(Mod mod);
	}
}
