//At the top of your plugin, you'll need to find the dll and add TwitchIntegration as a reference
[BepInDependency("Nexivyffulf.TwitchIntegration", BepInDependency.DependencyFlags.SoftDependency)]
//Your plugin
//...
[HarmonyPatch()]    //Makes Harmony run this patch
class Integration_Patcher
{
    static bool Prepare()   //Runs before attempting to patch
    {
        return Chainloader.PluginInfos.ContainsKey("Nexivyffulf.TwitchIntegration");    //Checks if this mod exists and stops patching if not
    }
    static MethodBase TargetMethod()    //The method you want to patch
    {
        TwitchIntegration.Integrations.integrations.Redeems.Add("myredeemname");    //The name of your redeem only
        return AccessTools.Method(typeof(TwitchIntegration.Integrations), nameof(TwitchIntegration.Integrations.CustomRedeem));
    }
    static void Postfix(string[] redeem, ref bool __result) //When one is redeemed, its arguments will show up as list
    {
        if(redeem[0] == "myredeemname") //Check if it is yours
        {
            //Your redeem code goes here
            //...
            __result = true;    //Set to true if you succeeded
        }
    }
}