using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace OWVoiceMod;

public class OWVoiceMod : ModBehaviour
{
	private new static IModHelper ModHelper;
	private static readonly Dictionary<string, string> assetPaths = new();
	private static string creditsAssetPath;
	public static AudioSource audioSource;

	public static int randomDialogueNum = -1;

	private static bool splashSkip;
	private static bool conversations;
	private static bool hearthianRecordings;
	public static bool nomaiRecordings;
	private static bool paperNotes;
	public static bool nomaiScrolls;
	public static bool nomaiComputers;
	public static bool owlkWriting;
	private static float volume;

	private void Start()
	{
		ModHelper = base.ModHelper;

		RegisterAssets(Path.Combine(ModHelper.Manifest.ModFolderPath, "Assets"));

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

		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

		LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
	}

	private static string FormatAssetName(string assetName) => assetName.Replace(" ", "").Replace("_", "").ToLower();

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

	public override object GetApi() => new OWVoiceModAPI();

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

	private static void OnAdvancePage(CharacterDialogueTree characterDialogueTree, string nodeName, int pageNum)
	{
		var xmlCharacterDialogueAsset = characterDialogueTree._xmlCharacterDialogueAsset;
		var characterName = characterDialogueTree._characterName;

		if (!conversations && characterName is not (CharacterDialogueTree.RECORDING_NAME or CharacterDialogueTree.SIGN_NAME)) return;
		if (!hearthianRecordings && characterName is CharacterDialogueTree.RECORDING_NAME) return;
		if (!paperNotes && characterName is CharacterDialogueTree.SIGN_NAME) return;

		UnloadAudio();

		var currentAssetName = $"{xmlCharacterDialogueAsset.name} {nodeName} {(randomDialogueNum != -1 ? randomDialogueNum : pageNum)}";

		LoadAudio(currentAssetName);
	}

	private static void OnEndConversation() => UnloadAudio();

	public static async void LoadAudio(string assetName)
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

	public static void UnloadAudio()
	{
		if (audioSource.clip == null) return;
		audioSource.Stop();
		Destroy(audioSource.clip);
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

		var clip = DownloadHandlerAudioClip.GetContent(uwr);

		// normalize
		// TODO remove after mixing
		var samples = new float[clip.samples * clip.channels];
		clip.GetData(samples, 0);
		var max = samples.Select(Mathf.Abs).Max();
		for (var i = 0; i < samples.Length; i++) samples[i] /= max;
		clip.SetData(samples, 0);

		return clip;
	}
}