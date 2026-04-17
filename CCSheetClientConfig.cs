using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace CCSheet
{
	class CCSheetClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[DefaultValue(false)]
		public bool HotbarShownByDefault { get; set; }
	}
}
