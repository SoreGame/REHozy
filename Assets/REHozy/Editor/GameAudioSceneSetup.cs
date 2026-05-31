#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using REHozy.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace REHozy.EditorTools
{
    public static class GameAudioSceneSetup
    {
        const string AudioFolder = "Assets/REHozy/Audio";
        const string ClipsFolder = "Assets/REHozy/Audio/Clips";
        const string CatalogPath = "Assets/REHozy/Audio/DefaultGameAudioCatalog.asset";
        const string MixerPath = "Assets/REHozy/Audio/REHozyAudioMixer.mixer";
        const string MixerTemplatePath = "Assets/REHozy/Editor/Templates/REHozyAudioMixerTemplate.mixer";
        const string PrefabPath = "Assets/REHozy/Audio/GameAudio.prefab";
        const string MenuPath = "REHozy/Setup Game Audio";

        [MenuItem(MenuPath)]
        public static void SetupGameAudio()
        {
            EnsureFolders();
            var catalog = EnsureCatalogAsset();
            var mixer = EnsureMixerAsset(out var ambientGroup, out var weatherGroup, out var sfxGroup);
            var prefab = EnsurePrefab(catalog, mixer, ambientGroup, weatherGroup, sfxGroup);
            EnsureSceneInstance(prefab, catalog, mixer, ambientGroup, weatherGroup, sfxGroup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (mixer == null)
            {
                Debug.LogWarning(
                    "Game Audio setup finished without an Audio Mixer. Sound still works via AudioSource volume. "
                    + "Run REHozy → Setup Game Audio again after creating "
                    + MixerPath
                    + " manually (Assets → Create → Audio Mixer), or place a template at "
                    + MixerTemplatePath
                    + ".");
            }
            else
            {
                Debug.Log("Game Audio setup complete. Assign clips in DefaultGameAudioCatalog.");
            }
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Audio"))
            {
                AssetDatabase.CreateFolder("Assets/REHozy", "Audio");
            }

            if (!AssetDatabase.IsValidFolder(ClipsFolder))
            {
                AssetDatabase.CreateFolder(AudioFolder, "Clips");
            }
        }

        static GameAudioCatalog EnsureCatalogAsset()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<GameAudioCatalog>();
            catalog.ambientLoop = new GameAudioClipEntry { loop = true, volume = 0.6f };
            catalog.rainLoop = new GameAudioClipEntry { loop = true, volume = 0.7f };
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        static AudioMixer EnsureMixerAsset(
            out AudioMixerGroup ambientGroup,
            out AudioMixerGroup weatherGroup,
            out AudioMixerGroup sfxGroup)
        {
            ambientGroup = null;
            weatherGroup = null;
            sfxGroup = null;

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                mixer = TryCopyMixerTemplate();
            }

            if (mixer == null)
            {
                mixer = TryCreateMixerViaUnityMenu();
            }

            if (mixer == null)
            {
                return null;
            }

            ambientGroup = AudioMixerEditorUtility.FindOrCreateGroup(mixer, "Ambient");
            weatherGroup = AudioMixerEditorUtility.FindOrCreateGroup(mixer, "Weather");
            sfxGroup = AudioMixerEditorUtility.FindOrCreateGroup(mixer, "SFX");

            EditorUtility.SetDirty(mixer);
            return mixer;
        }

        static AudioMixer TryCopyMixerTemplate()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerTemplatePath) == null)
            {
                return null;
            }

            if (!AssetDatabase.CopyAsset(MixerTemplatePath, MixerPath))
            {
                return null;
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        static AudioMixer TryCreateMixerViaUnityMenu()
        {
            var existingPaths = CollectMixerPathsInAudioFolder();

            var folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AudioFolder);
            if (folderAsset == null)
            {
                return null;
            }

            var previousSelection = Selection.activeObject;
            Selection.activeObject = folderAsset;
            EditorUtility.FocusProjectWindow();

            try
            {
                if (!EditorApplication.ExecuteMenuItem("Assets/Create/Audio Mixer"))
                {
                    Debug.LogWarning("Menu item Assets/Create/Audio Mixer was not found.");
                    return null;
                }

                AssetDatabase.Refresh();

                string createdPath = null;
                foreach (var path in CollectMixerPathsInAudioFolder())
                {
                    if (!existingPaths.Contains(path))
                    {
                        createdPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(createdPath))
                {
                    Debug.LogWarning("Unity did not create a new Audio Mixer in " + AudioFolder + ".");
                    return null;
                }

                if (createdPath != MixerPath)
                {
                    var moveError = AssetDatabase.MoveAsset(createdPath, MixerPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        Debug.LogWarning("Could not move Audio Mixer to " + MixerPath + ": " + moveError);
                        return AssetDatabase.LoadAssetAtPath<AudioMixer>(createdPath);
                    }
                }

                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            finally
            {
                Selection.activeObject = previousSelection;
            }
        }

        static HashSet<string> CollectMixerPathsInAudioFolder()
        {
            var paths = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioMixer", new[] { AudioFolder }))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths;
        }

        static GameObject EnsurePrefab(
            GameAudioCatalog catalog,
            AudioMixer mixer,
            AudioMixerGroup ambientGroup,
            AudioMixerGroup weatherGroup,
            AudioMixerGroup sfxGroup)
        {
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab != null)
            {
                var existingController = existingPrefab.GetComponent<GameAudioController>();
                if (existingController != null)
                {
                    ApplyControllerReferences(existingController, catalog, mixer, ambientGroup, weatherGroup, sfxGroup);
                    EditorUtility.SetDirty(existingPrefab);
                    PrefabUtility.SavePrefabAsset(existingPrefab);
                }

                return existingPrefab;
            }

            var root = new GameObject("GameAudio");
            var controller = root.AddComponent<GameAudioController>();
            if (root.GetComponent<ThirdPersonAudioListener>() == null)
            {
                root.AddComponent<ThirdPersonAudioListener>();
            }
            ApplyControllerReferences(controller, catalog, mixer, ambientGroup, weatherGroup, sfxGroup);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void EnsureSceneInstance(
            GameObject prefab,
            GameAudioCatalog catalog,
            AudioMixer mixer,
            AudioMixerGroup ambientGroup,
            AudioMixerGroup weatherGroup,
            AudioMixerGroup sfxGroup)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<GameAudioController>();
            if (existing != null)
            {
                ApplyControllerReferences(existing, catalog, mixer, ambientGroup, weatherGroup, sfxGroup);
                if (existing.GetComponent<ThirdPersonAudioListener>() == null)
                {
                    existing.gameObject.AddComponent<ThirdPersonAudioListener>();
                }
                EditorUtility.SetDirty(existing);
                return;
            }

            GameObject instance;
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                instance = new GameObject("GameAudio");
                instance.AddComponent<GameAudioController>();
            }

            instance.name = "GameAudio";
            var controller = instance.GetComponent<GameAudioController>();
            if (controller != null)
            {
                ApplyControllerReferences(controller, catalog, mixer, ambientGroup, weatherGroup, sfxGroup);
            }
        }

        static void ApplyControllerReferences(
            GameAudioController controller,
            GameAudioCatalog catalog,
            AudioMixer mixer,
            AudioMixerGroup ambientGroup,
            AudioMixerGroup weatherGroup,
            AudioMixerGroup sfxGroup)
        {
            controller.SetEditorReferences(catalog, mixer, ambientGroup, weatherGroup, sfxGroup);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("playAmbientOnStart").boolValue = true;
            serialized.FindProperty("rainEnabledOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static class AudioMixerEditorUtility
    {
        static readonly Type ControllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");

        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static AudioMixerGroup FindOrCreateGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null)
            {
                return null;
            }

            var existing = FindGroupByExactName(mixer, groupName);
            if (existing != null)
            {
                return existing;
            }

            if (ControllerType == null || !ControllerType.IsInstanceOfType(mixer))
            {
                return GetMasterGroup(mixer);
            }

            var createMethod = ControllerType.GetMethod(
                "CreateNewGroup",
                InstanceFlags,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            if (createMethod == null)
            {
                Debug.LogWarning($"CreateNewGroup not found. Using Master for '{groupName}'.");
                return GetMasterGroup(mixer);
            }

            return createMethod.Invoke(mixer, new object[] { groupName, false }) as AudioMixerGroup;
        }

        static AudioMixerGroup FindGroupByExactName(AudioMixer mixer, string groupName)
        {
            var groups = mixer.FindMatchingGroups(groupName);
            for (var i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].name == groupName)
                {
                    return groups[i];
                }
            }

            return null;
        }

        static AudioMixerGroup GetMasterGroup(AudioMixer mixer)
        {
            var masterGroups = mixer.FindMatchingGroups("Master");
            return masterGroups.Length > 0 ? masterGroups[0] : null;
        }
    }
}
#endif
