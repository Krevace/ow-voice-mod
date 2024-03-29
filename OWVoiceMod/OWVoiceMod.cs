﻿using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
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

		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

		LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
	}

	private static string FormatAssetName(string assetName) => assetName.Replace(" ", "").Replace("_", "").ToLower();

	public static void RegisterAssets(string assetsFolder)
	{
		try
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
				var assetName = Path.GetFileNameWithoutExtension(assetPath);
				assetPaths[FormatAssetName(assetName)] = assetPath;
			}
		}
		catch { ModHelper.Console.WriteLine("Error finding and registering assests!", MessageType.Error); }
	}

	public override object GetApi() => new OWVoiceModAPI();

	public override void Configure(IModConfig config)
	{
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
				audioSource.outputAudioMixerGroup = Locator.GetAudioMixer().GetAudioMixerGroup(OWAudioMixer.TrackName.Player);
				// Scales based on master volume

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

		path = $"file:///{path.Replace("+", "%2B")}";
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
