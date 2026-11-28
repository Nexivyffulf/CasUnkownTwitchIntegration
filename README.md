#### **Installation:**

Add it to "...BepInEx/plugins". Preferably in its own folder for organization.



#### **Usage:**

To enable, find the setting in the settings menu under gameplay. Pressing the button will open a webpage. There authorize "Grey Planet Live Feed". You will be asked to log into Twitch if you are not already. You need to be a twitch affiliate for this mod to work (and have custom redeems in the first place.)



To skip having to open a new page and login/authorize again, you can press "stay logged in", which will save an OAuth key for next time on successful login. The redeems will only be read if you are loaded in. Any redeemed on pause will go off when you un-pause.



It is recommended to look below and set up your own redeems before using this mod, though my demo template will work, it does not contain every redeem, and the costs may not match what you want. As an extra, you can mass refund these in Twitch to give your viewers back their points.



###### Commands:



*TI\_TestRedeem \<redeem syntax>*

&#x20;   Allows you to see what a redeem will do without the need of an actual channel point reward, you still need to

&#x20;   at least be connected to Twitch. You don't have to be a Twitch affiliate.



##### **Adding redemptions:**

Look at "Redeems.txt" in there you will see redeems laid out with keywords at the end. They will work but you really should make your own. They follow the pattern (Title):(prompt):(cost):(delay):(redeem syntax). Prompt is the name for the description. The title and prompt can be anything you want as long as they don't contain ':'(and don't break Twitch's rules). Add it to the list here and on connect a matching one will appear on your channel, when you connect to Twitch. They will be removed when you either press the connect button again or close the game. You can configure them to disable instead if you want to add pictures and colours to them. Disabled redeems are hidden from your audience. Note you can only have 50 channel point rewards on your channel.



**braindamage**

    causes a random brain damage effect to play



**command \<command>**

    runs the specified command, it can still disable achievements



**drainbatteries**

    drains all your batteries by 33%



**feed \<hunger> \<thirst>**

    gives the respective values, can be negative



**give \<item>**

    gives the player the item



**give random \<category>**

&#x20;   gives a random item, you can add a category to make it be from a specific pool:

        medical: medical items not including drugs

        food: processed food specifically

        water: drinkable containers, like Iced Tea

        tool: cutting, hammering, digging, excludes all titanium

        drug: drugs

        container: item container, bags

        utility: misc items, lock picking kit, light bulb

        custom: items excluded from above, ranges from foliage and Geo-fruit to the titanium items



**light \<level>**

&#x20;   this is the ambient light setting as a reminder of it's values: none, enough, bright, or random, which swaps it off your current one



**opiate \<opiate> \<tolerance> \<antagonist>**

&#x20;   adds to your opiate reception, you can use all 3 to tailor how you want it to act

        opiate: how much opiates are in your system, this being negative is not the same as antagonist

        tolerance: how much tolerance you have, set equal to opiate to make it a slow reveal

        antagonist: opiate antagonist, it is approximately 8 to 1 effectiveness



**playmusic \<song>**

&#x20;   plays the song, include the full name including file extension, or set to random for a random song



**randomise liquids \[values...]**

&#x20;   randomises all liquids in the game, include all that you want to happen in values, will reset when you start a run

        dangerous: include dangerous liquids

        localename: displayed name and description

        valueperliter: fair market value

        ondrink: what it does on drink

        onHealthUse: what it does when used on your body

        healthUsable: if it can be used on your body

        injectable: if you are supposed to inject it for the effect

        injectionsickness: sickness for injecting

        qualities: crafting qualities



**spawnelder \<fake>**

&#x20;   spawns a thornbackelder 100 units away from you in a random direction, just in range to horrify but can't see you

&#x20;   yet, add fake to just horrify, it will also steal the title of the real redeem if you have it



**swaphands**

&#x20;   swap your current main hand, you won't get the message



**teleport**

&#x20;   teleports you, same as the crystal, it is not my fault if this disintegrates you



**trip**

&#x20;   forces you into a ragdoll





##### **Config:**

If you don't trust me with adding and removing these redeems, you can disable it in the config file found in "...BepInEx/configs".
There you will also find a setting to have redeems be disabled instead of being removed.
This is so you can add any pictures or custom colours along with anything else I don't support in Redeems.txt.
The only thing that has to match in Redeems.txt is the title.

There is also 3 other entries that I store stuff in, please don't try to break them.



##### **Github:**

For suggestions, bug reporting, or looking at the source code.
https://github.com/Nexivyffulf/CasUnkownTwitchIntegration

