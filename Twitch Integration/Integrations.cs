using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;

namespace TwitchIntegration;

public class Integrations : MonoBehaviour
{
	public static Integrations integrations = new();
	public bool GiveItem(string item)
	{
		GameObject gameObject = Utils.Create(item, PlayerCamera.main.body.transform.position, 0f);
		if (!gameObject)
		{
			Plugin.Logger.LogError("Could not find item!");
			return false;
		}
		for (int i = 0; i < 5; i++)
			if (PlayerCamera.main.body.slots[i].canPickUp)
			{
				PlayerCamera.main.body.PickUpItem(gameObject.GetComponent<Item>(), i, true);
				break;
			}
		return true;
	}
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
	public bool CustomRedeem(string[] redeem)   //To make one: postfix and read redeem, if it is yours and it worked, only then change the return
	{
		Plugin.Logger.LogInfo($"Passing redeem: {string.Join(" ", redeem[0])}");
		return false;
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
	public bool DrainBatteries()
	{
		foreach (Item item in PlayerCamera.main.body.GetAllItemsThorough())
		{
			if ((bool)item.battery)
			{
				item.battery.DrainCharge(0.33f);
			}
			if (item.Stats is BatteryInfo)
			{
				item.condition -= 0.33f;
			}
		}
		Sound.Play("crystalemp", PlayerCamera.main.body.transform.position, twoDimensional: true, pitchShift: false);
		PlayerCamera.main.shaker.Shake();
		return true;
	}
	public bool Teleport()
	{
		for (int i = 0; i < 1000; i++)
		{
			Vector2 vector = (Vector2)PlayerCamera.main.body.transform.position + Random.insideUnitCircle * 64f;
			if (vector.x < 0L - WorldGeneration.world.halfWidth + 10f)
				vector.x = 0L - WorldGeneration.world.halfWidth + 10f;
			if (vector.x > WorldGeneration.world.halfWidth - 10f)
				vector.x = WorldGeneration.world.halfWidth - 10f;
			if (!Physics2D.OverlapPoint(vector, LayerMask.GetMask("Ground")))
			{
				RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, 999f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					Sound.Play("observerlaugh", Vector2.zero, twoDimensional: true, pitchShift: false, null, 1f, 1f, noReverb: true, ignoreMixer: true);
					PlayerCamera.main.StartCoroutine("FlashBrief");
					PlayerCamera.main.body.consciousness = 100f;
					PlayerCamera.main.body.shock = 0f;
					PlayerCamera.main.body.rb.velocity = Vector2.zero;
					PlayerCamera.main.body.Stand();
					PlayerCamera.main.body.transform.position = raycastHit2D.point + Vector2.up * 3f;
					break;
				}
			}
		}
		return true;
	}
	public bool Trip()
	{
		PlayerCamera.main.body.Ragdoll();
		return true;
	}
	public bool Spawnelder(bool fake)
	{
		if (fake)
			Rattle();
		else
			Instantiate(Resources.Load("thornbackelder"), (Vector2)PlayerCamera.main.body.transform.position + Random.insideUnitCircle.normalized * 100f, Quaternion.identity);
		return true;
	}

	public async void Rattle()
	{
		for (int i = 0; i < 20; i++)
		{
			PlayerCamera.main.body.adrenaline = 100f;
			PlayerCamera.main.body.horrifiedLevel = 50f;
			if (PlayerCamera.main.body.sleeping)
			{
				PlayerCamera.main.body.WakeUp();
				PlayerCamera.main.body.energy = Mathf.Max(PlayerCamera.main.body.energy, 10f);
			}
			await Task.Delay(500);
		}

	}
	public bool Command(string command)
	{
		ConsoleScript.instance.ExecuteCommand(command);
		return true;
	}
	public static Dictionary<string, LiquidType> originalliquids = Liquids.Registry;
	public bool RandomiseLiquids(string values)
	{
		Dictionary<string, LiquidType> registry = Liquids.Registry;

		if (values.Contains("dangerous"))
			foreach (string dangers in Liquids.DangerList)
				registry.Remove(dangers);

		LiquidType[] liquids = [.. registry.Values];
		string[] ids = [.. registry.Keys];
		for (int i = liquids.Length - 1; i > 1; i--)
		{
			int j = Random.Range(0, i + 1);
			(liquids[j], liquids[i]) = (liquids[i], liquids[j]);
		}
		bool localeName = values.Contains("localename");
		bool color = values.Contains("color");
		bool valuePerLiter = values.Contains("valueperliter");
		bool onDrink = values.Contains("ondrink");
		bool injectionSickness = values.Contains("injectionsickness");
		bool qualities = values.Contains("qualities");
		for (int i = 0; i < liquids.Length; i++)
		{
			if (localeName)
				Liquids.Registry[ids[i]].localeName = liquids[i].localeName;
			if (color)
				Liquids.Registry[ids[i]].color = liquids[i].color;
			if (valuePerLiter)
				Liquids.Registry[ids[i]].valuePerLiter = liquids[i].valuePerLiter;
			if (onDrink)
				Liquids.Registry[ids[i]].onDrink = liquids[i].onDrink;
			if (injectionSickness)
				Liquids.Registry[ids[i]].injectionSickness = liquids[i].injectionSickness;
			if (qualities)
				Liquids.Registry[ids[i]].qualities = liquids[i].qualities;
		}
		return true;
	}
	public bool Swaphands()
	{
		PlayerCamera.main.body.handSlot = (PlayerCamera.main.body.handSlot == 0) ? 1 : 0;
		PlayerCamera.main.handSwapImage.sprite = PlayerCamera.main.handSwapSprites[PlayerCamera.main.body.handSlot];
		return true;
	}
	public bool Opiate(int opiate, int tolerance, int antagonist)
	{
		PlayerCamera.main.body.GetOrAddComponent<Painkillers>().opiateAmount += opiate;
		PlayerCamera.main.body.GetOrAddComponent<Painkillers>().opiateTolerance += tolerance;
		PlayerCamera.main.body.GetOrAddComponent<Painkillers>().antagonistAmount += antagonist;
		return true;
	}
	public List<AudioClip> clips = [.. Resources.LoadAll<AudioClip>("Sounds/music")];   //Might as wel get them out of the way here
	public bool PlayMusic()
	{
		MusicManager.main.PlaySong(clips[Random.Range(0, clips.Count - 1)]);
		return true;
	}
	public bool PlayMusic(string song)
	{
		AudioClip clip = clips[0];
		for (int i = 0; i < clips.Count; i++)
			if (clips[i].name == song)
				clip = clips[i];
		MusicManager.main.PlaySong(clip);
		return true;
	}
	public bool Feed(int hunger, int thirst)
	{
		PlayerCamera.main.body.hunger += hunger;
		PlayerCamera.main.body.thirst += thirst;
		return true;
	}
	readonly public List<string> lightlevels = ["none", "enough", "bright"];
	public bool Light(string setting)
	{
		if (setting == "random")
			WorldGeneration.runSettings["ambientlight"] = (WorldGeneration.GetRunSettingInt("ambientlight") + Random.Range(0, 2)) % 3;
		WorldGeneration.runSettings["ambientlight"] = lightlevels.IndexOf(setting);
		return true;
	}
	public List<string> Redeems =
	[
		"NOTREAL",	//Caps so it will never legitamitely be chosen
		"braindamage",
		"command",
		"drainbatteries",
		"feed",
		"give",
		"light",
		"opiate",
		"playmusic",
		"randomiseliquids",
		"spawnelder",
		"swaphands",
		"teleport",
		"trip"
	];
}