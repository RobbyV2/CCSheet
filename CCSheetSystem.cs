using Terraria;
using Terraria.ModLoader;

namespace CCSheet
{
	internal class CCSheetSystem : ModSystem
	{
		public override void PostSetupRecipes() {
			CCSheet.instance.SetupUI();
		}
	}
}
