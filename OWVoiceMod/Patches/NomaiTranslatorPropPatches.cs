using HarmonyLib;

namespace OWVoiceMod.Patches;

[HarmonyPatch(typeof(NomaiTranslatorProp))]
public class NomaiTranslatorPropPatches
{
	private static string currentTextName;
	private static string oldTextName;

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.DisplayTextNode))]
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

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.SetTargetingGhostText))]
	public static void SetTargetingGhostText(NomaiTranslatorProp __instance, bool isTargetingGhostText)
	{
		if (__instance._isTargetingGhostText == isTargetingGhostText) return;
		if (OWVoiceMod.owlkWriting && isTargetingGhostText && !__instance._isTooCloseToTarget)
		{
			OWVoiceMod.UnloadAudio();
			OWVoiceMod.audioSource.loop = true;
			OWVoiceMod.LoadAudio("OwlkStatic");
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.SetTooCloseToTarget))]
	public static void SetTooCloseToTarget(NomaiTranslatorProp __instance, bool value)
	{
		if (__instance._isTooCloseToTarget == value) return;
		if (value)
		{
			OWVoiceMod.UnloadAudio();
			oldTextName = null;
		}
		else
		{
			// Fixes ghost audio bugs when Translator Auto-Equip is disabled
			__instance._isTargetingGhostText = false;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.ClearNomaiText))]
	public static void ClearNomaiText(NomaiTranslatorProp __instance)
	{
		OWVoiceMod.UnloadAudio();
		oldTextName = null;
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.OnEquipTool))]
	public static void OnEquipTool(NomaiTranslatorProp __instance) =>
		// Fixes ghost audio bugs when Translator Auto-Equip is disabled
		__instance._isTargetingGhostText = false;

	[HarmonyPrefix]
	[HarmonyPatch(nameof(NomaiTranslatorProp.OnUnequipTool))]
	public static void OnUnequipTool()
	{
		OWVoiceMod.UnloadAudio();
		oldTextName = null;
	}
}