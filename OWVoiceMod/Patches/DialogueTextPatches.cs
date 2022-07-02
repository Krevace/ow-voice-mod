using System.Collections.Generic;

namespace OWVoiceMod.Patches;

public class DialogueTextPatches
{
	public static bool GetDisplayStringList(DialogueText __instance, ref List<string> __result)
	{
		// Ensures that the correct ID is used if dialogue uses randomization (ex. Gabbro intro lines)
		if (__instance._randomize)
		{
			OWVoiceMod.randomDialogueNum = UnityEngine.Random.Range(0, __instance._listTextBlocks.Count);
			__result = __instance._listTextBlocks[OWVoiceMod.randomDialogueNum].listPageText;
			return false;
		}

		OWVoiceMod.randomDialogueNum = -1;
		return true;
	}
}
