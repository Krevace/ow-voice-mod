using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace OWVoiceMod
{
	public class OWVoiceMod : ModBehaviour
	{
		private new static IModHelper ModHelper;
		private static readonly Dictionary<string, string> assetPaths = new();
		private static string creditsAssetPath;
		private static AudioSource audioSource;

		private static string currentTextName;
		private static string oldTextName;
		private static int randomDialogueNum = -1;

		private static bool splashSkip;
		private static bool conversations;
		private static bool hearthianRecordings;
		private static bool nomaiRecordings;
		private static bool paperNotes;
		private static bool nomaiScrolls;
		private static bool nomaiComputers;
		private static bool owlkWriting;
		private static float volume;

		private void Start()
		{
			ModHelper = base.ModHelper;

			RegisterAssets(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets"));

			if (splashSkip)
			{
				// Copied from https://github.com/Vesper-Works/OuterWildsOnline/blob/master/OuterWildsOnline/ConnectionController.cs#L106-L119
				// Skip flash screen.
				var titleScreenAnimation = FindObjectOfType<TitleScreenAnimation>();
				titleScreenAnimation._fadeDuration = 0;
				titleScreenAnimation._gamepadSplash = false;
				titleScreenAnimation._introPan = false;
				titleScreenAnimation.Invoke("FadeInTitleLogo");

				// Skip menu fade.
				var titleAnimationController = FindObjectOfType<TitleAnimationController>();
				titleAnimationController._logoFadeDelay = 0.001f;
				titleAnimationController._logoFadeDuration = 0.001f;
				titleAnimationController._optionsFadeDelay = 0.001f;
				titleAnimationController._optionsFadeDuration = 0.001f;
				titleAnimationController._optionsFadeSpacing = 0.001f;
			}

			ModHelper.HarmonyHelper.AddPrefix<DialogueText>(nameof(DialogueText.GetDisplayStringList), typeof(OWVoiceMod), nameof(GetDisplayStringList));
			ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.DisplayTextNode), typeof(OWVoiceMod), nameof(DisplayTextNode));
			ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.SetTargetingGhostText), typeof(OWVoiceMod), nameof(SetTargetingGhostText));
			ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.SetTooCloseToTarget), typeof(OWVoiceMod), nameof(SetTooCloseToTarget));
			ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.ClearNomaiText), typeof(OWVoiceMod), nameof(ClearNomaiText));
			ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.OnUnequipTool), typeof(OWVoiceMod), nameof(OnUnequipTool));

			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
		}

		private static string FormatAssetName(string assetName) => assetName.Replace(" ", "").ToLower();

		public static void RegisterAssets(string assetsFolder)
		{
			foreach (var assetPath in Directory.EnumerateFiles(assetsFolder, "credits.xml", SearchOption.AllDirectories))
			{
				creditsAssetPath = assetPath;
				break;
			}

			foreach (var assetPath in Directory.EnumerateFiles(assetsFolder, "*.mp3", SearchOption.AllDirectories)
						.Concat(Directory.EnumerateFiles(assetsFolder, "*.wav", SearchOption.AllDirectories))
						.Concat(Directory.EnumerateFiles(assetsFolder, "*.ogg", SearchOption.AllDirectories)))
			{
				// Conjoins audio files of the same content with different names using &
				foreach (var assetName in Path.GetFileNameWithoutExtension(assetPath).Split('&'))
				{
					assetPaths[FormatAssetName(assetName)] = assetPath;
				}
			}
		}

		public override object GetApi() => new VoiceModApi();

		public override void Configure(IModConfig config)
		{
			splashSkip = config.GetSettingsValue<bool>("splashSkip");
			conversations = config.GetSettingsValue<bool>("conversations");
			hearthianRecordings = config.GetSettingsValue<bool>("hearthianRecordings");
			nomaiRecordings = config.GetSettingsValue<bool>("nomaiRecordings");
			paperNotes = config.GetSettingsValue<bool>("paperNotes");
			nomaiScrolls = config.GetSettingsValue<bool>("nomaiScrolls");
			nomaiComputers = config.GetSettingsValue<bool>("nomaiComputers");
			owlkWriting = config.GetSettingsValue<bool>("owlkWriting");
			volume = config.GetSettingsValue<float>("volume");
			if (audioSource != null) audioSource.volume = volume;
		}

		private static void OnCompleteSceneLoad(OWScene orignalScene, OWScene loadScene)
		{
			if (loadScene is OWScene.Credits_Fast or OWScene.Credits_Final)
			{
				var creditsAsset = FindObjectOfType<Credits>()._creditsAsset;
				try { creditsAsset.xml = new TextAsset(File.ReadAllText(creditsAssetPath)); }
				catch { ModHelper.Console.WriteLine("Credits file not found!", MessageType.Error); }
			}
			else if (loadScene is OWScene.SolarSystem or OWScene.EyeOfTheUniverse)
			{
				// Gives time for Start to run
				ModHelper.Events.Unity.FireOnNextUpdate(() =>
				{
					audioSource = Locator.GetPlayerBody().gameObject.AddComponent<AudioSource>();
					audioSource.volume = volume;
					audioSource.outputAudioMixerGroup = Locator.GetAudioMixer().GetAudioMixerGroup(OWAudioMixer.TrackName.Environment);
					// Placed on the Environment track to get environmental effects like reverb

					var characterDialogueTrees = Resources.FindObjectsOfTypeAll<CharacterDialogueTree>();
					foreach (var characterDialogueTree in characterDialogueTrees)
					{
						characterDialogueTree.OnAdvancePage += (nodeName, pageNum) => OnAdvancePage(characterDialogueTree, nodeName, pageNum);
						characterDialogueTree.OnEndConversation += OnEndConversation;
					}
				});
			}
		}

		private static bool GetDisplayStringList(DialogueText __instance, ref List<string> __result)
		{
			// Ensures that the correct ID is used if dialogue uses randomization (ex. Gabbro intro lines)
			if (__instance._randomize)
			{
				randomDialogueNum = UnityEngine.Random.Range(0, __instance._listTextBlocks.Count);
				__result = __instance._listTextBlocks[randomDialogueNum].listPageText;
				return false;
			}

			randomDialogueNum = -1;
			return true;
		}

		private static void OnAdvancePage(CharacterDialogueTree characterDialogueTree, string nodeName, int pageNum)
		{
			var xmlCharacterDialogueAsset = characterDialogueTree._xmlCharacterDialogueAsset;
			var characterName = characterDialogueTree._characterName;

			if (!conversations && characterName != "NOTE" && characterName != "RECORDING") return;
			if (!hearthianRecordings && characterName == "RECORDING") return;
			if (!paperNotes && characterName == "NOTE") return;

			UnloadAudio();

			var currentAssetName = $"{xmlCharacterDialogueAsset.name} {nodeName} {(randomDialogueNum != -1 ? randomDialogueNum : pageNum)}";

			LoadAudio(currentAssetName);
		}

		private static void OnEndConversation() => UnloadAudio();

		private static void DisplayTextNode(NomaiTranslatorProp __instance)
		{
			var nomaiText = __instance._nomaiTextComponent;
			var currentTextID = __instance._currentTextID;

			string currentAssetName;
			if (nomaiText is NomaiComputer or NomaiVesselComputer)
			{
				if (!nomaiComputers) return;
				if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out _)) currentAssetName = "NomaiWarpComputer";
				else currentAssetName = nomaiText._nomaiTextAsset.name;
				currentTextName = $"{currentAssetName} {currentTextID}";
			}
			else
			{
				if (!nomaiScrolls && nomaiText is NomaiWallText) return;
				if (!nomaiRecordings && nomaiText is not NomaiWallText) return;
				currentAssetName = nomaiText._nomaiTextAsset.name;
				currentTextName = $"{currentAssetName} {currentTextID}";
			}

			if (currentTextName == oldTextName) return;

			UnloadAudio();

			if (nomaiText.IsTranslated(currentTextID))
			{
				LoadAudio(currentTextName);
			}
			else
			{
				oldTextName = null;
			}
		}

		private static void SetTargetingGhostText(NomaiTranslatorProp __instance, bool isTargetingGhostText)
		{
			if (__instance._isTargetingGhostText == isTargetingGhostText) return;
			if (owlkWriting && isTargetingGhostText)
			{
				UnloadAudio();
				audioSource.loop = true;
				LoadAudio("OwlkStatic");
			}
		}

		private static void SetTooCloseToTarget(NomaiTranslatorProp __instance, bool value)
		{
			if (__instance._isTooCloseToTarget == value) return;
			if (value)
			{
				UnloadAudio();
				oldTextName = null;
			}
		}

		private static void ClearNomaiText(NomaiTranslatorProp __instance)
		{
			if (__instance._nomaiTextComponent == null) return;
			UnloadAudio();
			oldTextName = null;
		}

		private static void OnUnequipTool()
		{
			UnloadAudio();
			oldTextName = null;
		}

		private static async void LoadAudio(string assetName)
		{
			if (assetPaths.TryGetValue(FormatAssetName(assetName), out var assetPath))
			{
				ModHelper.Console.WriteLine($"Found audio for {assetName}", MessageType.Success);
				audioSource.clip = await GetAudio(assetPath);
				if (volume > 0 && audioSource.clip != null) audioSource.Play();
			}
			else
			{
				ModHelper.Console.WriteLine($"Couldn't find audio for {assetName}", MessageType.Error);
			}
		}

		private static void UnloadAudio()
		{
			audioSource.Stop();
			if (audioSource.clip != null) Destroy(audioSource.clip);
			audioSource.clip = null;
			audioSource.loop = false;
		}

		private static async Task<AudioClip> GetAudio(string path)
		{
			var audioType = Path.GetExtension(path) switch
			{
				".ogg" => UnityEngine.AudioType.OGGVORBIS,
				".wav" => UnityEngine.AudioType.WAV,
				".mp3" => UnityEngine.AudioType.MPEG,
				_ => throw new ArgumentOutOfRangeException()
			};

			using var uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType);

			uwr.SendWebRequest();
			while (!uwr.isDone) await Task.Yield();

			if (uwr.isNetworkError || uwr.isHttpError)
			{
				ModHelper.Console.WriteLine(uwr.error, MessageType.Error);
				return null;
			}

			return DownloadHandlerAudioClip.GetContent(uwr);
		}
	}

	public class VoiceModApi
	{
		public void RegisterAssets(string assetsFolder) => OWVoiceMod.RegisterAssets(assetsFolder);
	}
}
