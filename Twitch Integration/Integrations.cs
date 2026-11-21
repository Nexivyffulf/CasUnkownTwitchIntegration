using System.Collections.Generic;
using UnityEngine;

namespace TwitchIntegration;

internal class Integrations : MonoBehaviour
{
    public static Integrations CUIntegrations { get; }

    public bool GiveItem(string item)
    {
        GameObject gameObject = Utils.Create(item, PlayerCamera.main.body.transform.position, 0f);
        if (!gameObject)
        {
            Plugin.Logger.LogError("Could not find item!");
            return false;
        }
        PlayerCamera.main.body.AutoPickUpItem(gameObject.GetComponent<Item>());
        return true;
    }

    public bool GiveRandomItem(string category)
    {
        if (!RandomItemRedeems.Contains(category))  //If not known, be all
        {
            List<string> Items = [.. Item.GlobalItems.Keys];    //This should make the odds way more even than how traders do it
            return GiveItem(Items.PickRandom());
        }
        else
            return GiveItem(ItemLootPool.pool[category].PickRandom());
    }

    readonly List<string> RandomItemRedeems =
    [
        "medical",
        "food",
        "water",
        "tool",
        "drug",
        "container",
        "utility",
        "custom"
    ];
}