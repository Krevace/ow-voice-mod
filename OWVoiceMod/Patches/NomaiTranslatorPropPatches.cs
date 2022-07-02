namespace OWVoiceMod.Patches;

public class NomaiTranslatorPropPatches
{
	private static string currentTextName;
	private static string oldTextName;

	public static void DisplayTextNode(NomaiTranslatorProp __instance)
	{
		var nomaiText = __instance._nomaiTextComponent;
		var currentTextID = __instance._currentTextID;

		string currentAssetName;
		if (nomaiText is NomaiComputer or NomaiVesselComputer)
		{
			if (!OWVoiceMod.nomaiComputers) return;
			if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out _)) currentAssetName = "NomaiWarpComputer";
			else currentAssetName = nomaiText._nomaiTextAsset.name;
			currentTextName = $"{currentAssetName} {currentTextID}";
		}
		else
		{
			if (!OWVoiceMod.nomaiScrolls && nomaiText is NomaiWallText) return;
			if (!OWVoiceMod.nomaiRecordings && nomaiText is not NomaiWallText) return;
			currentAssetName = nomaiText._nomaiTextAsset.name;
			currentTextName = $"{currentAssetName} {currentTextID}";
		}

		if (currentTextName == oldTextName) return;

		OWVoiceMod.UnloadAudio();

		if (nomaiText.IsTranslated(currentTextID))
		{
			OWVoiceMod.LoadAudio(currentTextName);
			oldTextName = currentTextName;
		}
		else
		{
			oldTextName = null;
		}
	}

	public static void SetTargetingGhostText(NomaiTranslatorProp __instance, bool isTargetingGhostText)
	{
		if (__instance._isTargetingGhostText == isTargetingGhostText) return;
		if (OWVoiceMod.owlkWriting && isTargetingGhostText)
		{
			OWVoiceMod.UnloadAudio();
			OWVoiceMod.audioSource.loop = true;
			OWVoiceMod.LoadAudio("OwlkStatic");
		}
	}

	public static void SetTooCloseToTarget(NomaiTranslatorProp __instance, bool value)
	{
		if (__instance._isTooCloseToTarget == value) return;
		if (value)
		{
			OWVoiceMod.UnloadAudio();
			oldTextName = null;
		}
	}

	public static void ClearNomaiText(NomaiTranslatorProp __instance)
	{
		if (__instance._nomaiTextComponent == null) return;
		OWVoiceMod.UnloadAudio();
		oldTextName = null;
	}

	public static void OnUnequipTool()
	{
		OWVoiceMod.UnloadAudio();
		oldTextName = null;
	}
}
