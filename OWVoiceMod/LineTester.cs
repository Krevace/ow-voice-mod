using System;
using UnityEngine;
using static OWVoiceMod.OWVoiceMod;

namespace OWVoiceMod;

public static class LineTester
{
	public static void Run()
	{
		ModHelper.Console.WriteLine("testing trees");
		foreach (var characterDialogueTree in Resources.FindObjectsOfTypeAll<CharacterDialogueTree>())
		{
			if (!characterDialogueTree || !characterDialogueTree._xmlCharacterDialogueAsset || characterDialogueTree._xmlCharacterDialogueAsset.text == null) continue;
			
			var xmlCharacterDialogueAsset = characterDialogueTree._xmlCharacterDialogueAsset;

			string nodeName = null;
			var pageNum = -1;

			var lines = xmlCharacterDialogueAsset.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines)
			{
				var trim = line.Trim();
				if (trim.StartsWith("<Name>") && trim.EndsWith("</Name>"))
				{
					nodeName = trim.Replace("<Name>", "").Replace("</Name>", "");
					pageNum = -1;
				}
				else if (trim.StartsWith("<Page>") && trim.EndsWith("</Page>"))
				{
					pageNum++;
				}
				else
				{
					continue;
				}

				if (nodeName == null || pageNum == -1) continue;

				var assetName = $"{xmlCharacterDialogueAsset.name} {nodeName} {pageNum}";
				if (!assetPaths.TryGetValue(FormatAssetName(assetName), out _))
					ModHelper.Console.WriteLine($"MISSING {assetName}");
			}
		}


		ModHelper.Console.WriteLine("testing nomai text");
		foreach (var nomaiText in Resources.FindObjectsOfTypeAll<NomaiText>())
		{
			if (!nomaiText || !nomaiText._nomaiTextAsset || nomaiText._nomaiTextAsset.text == null) continue;
			
			var nomaiTextAsset = nomaiText._nomaiTextAsset;

			var currentTextID = -1;

			var lines = nomaiTextAsset.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines)
			{
				var trim = line.Trim();
				if (trim.StartsWith("<ID>") && trim.EndsWith("</ID>"))
				{
					currentTextID = int.Parse(trim.Replace("<ID>", "").Replace("</ID>", ""));
				}
				else
				{
					continue;
				}

				if (currentTextID == -1) continue;

				string assetName;
				if (nomaiText is NomaiComputer or NomaiVesselComputer)
				{
					if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out _)) assetName = "NomaiWarpComputer";
					else assetName = nomaiText._nomaiTextAsset.name;
					assetName = $"{assetName} {currentTextID}";
				}
				else
				{
					assetName = nomaiText._nomaiTextAsset.name;
					assetName = $"{assetName} {currentTextID}";
				}
				if (!assetPaths.TryGetValue(FormatAssetName(assetName), out _))
					ModHelper.Console.WriteLine($"MISSING {assetName}");
			}
		}
	}
}
