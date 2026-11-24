using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwitchIntegration;

public class Integrations : MonoBehaviour
{
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

    public bool BrainDamage()
    {
        switch (Random.Range(0, 15))
        {
            case 0:
                PlayerCamera.main.body.talker.Talk(Locale.GetCharacter("hitbycreature"));
                PlayerCamera.main.body.eyeScareTime = 5f;
                PlayerCamera.main.threatMusicTime = 8f;
                Sound.Play("spiderbite", transform.position);
                PlayerCamera.main.body.venomFake += 20f;
                PlayerCamera.main.body.limbs.Where(x => !x.dismembered && !x.prostheticInfo.hasProsthetic).ToList().PickRandom().pain += 35f;
                PlayerCamera.main.body.adrenaline += 40f;
                PlayerCamera.main.SetTimeScale(PlayerCamera.SpeedType.Normal, switchSound: false);
                break;
            case 1:
                PlayerCamera.main.radialOpen = !PlayerCamera.main.radialOpen;
                break;
            case 2:
                PlayerCamera.main.body.UseItemInHand();
                break;
            case 3:
                PlayerCamera.main.body.Jump();
                break;
            case 4:
                PlayerCamera.main.body.attackCooldown = 15f;
                PlayerCamera.main.lastAttackCool = 15f;
                break;
            case 5:
                PlayerCamera.main.ToggleWoundView();
                break;
            case 6:
                PlayerCamera.main.OpenCraftScreen();
                break;
            case 7:
                Sound.Play("sonarcharge", transform.position, twoDimensional: true, pitchShift: false);
                PlayerCamera.main.WarnSoundCannon((Vector2)transform.position + Random.insideUnitCircle * 64f);
                break;
            case 8:
                Sound.Play("turretsee", transform.position, twoDimensional: true, pitchShift: false);
                PlayerCamera.main.body.talker.Talk("..!");
                break;
            case 9:
                Sound.Play("mine", transform.position);
                break;
            case 10:
                StartCoroutine("BrainControlReverse");
                break;
            case 11:
                StartCoroutine("BrainQuake");
                break;
            case 12:
                PlayerCamera.main.body.vomiter.Vomit();
                break;
            case 13:
                if (Random.value < 0.75f)
                {
                    PlayerCamera.main.body.DropItem(0);
                }
                if (Random.value < 0.75f)
                {
                    PlayerCamera.main.body.DropItem(1);
                }
                if (Random.value < 0.75f)
                {
                    PlayerCamera.main.body.DropItem(2);
                }
                PlayerCamera.main.body.talker.Talk("..?");
                break;
            case 14:
                StartCoroutine(BrainDamageRagdoll());
                break;
            case 15:
                PlayerCamera.main.StartCoroutine("FlashBrain");
                break;
        }
        return true;
    }

    private IEnumerator BrainDamageRagdoll()
    {
        float timer = 0f;
        while (timer < 5f)
        {
            timer += Time.deltaTime;
            PlayerCamera.main.body.brainShakeIntensity = timer * 0.05f;
            yield return null;
        }
        PlayerCamera.main.body.brainShakeIntensity = 0f;
        PlayerCamera.main.body.Ragdoll();
    }

    readonly public List<string> Redeems =
    [
        "braindamage",
        "give",
        "teleport"
    ];
    readonly public List<string> RandomItemRedeems =
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