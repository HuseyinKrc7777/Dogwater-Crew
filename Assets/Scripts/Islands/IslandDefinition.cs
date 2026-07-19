using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// One island, defined once. Everything that needs to talk about an island - the entry trigger on its
// silhouette, its arrival point, its quest spawn points, the quest generator's catalog - references this
// asset instead of repeating its name as a string.
//
// That is the whole point: an island's identity used to be a string typed into four different inspectors
// (and into every spawn point). A single wrong letter in any of them failed silently - the NPC simply
// never appeared, with no error anywhere. An asset reference cannot be mistyped.
[CreateAssetMenu(menuName = "Dogwater/Islands/Island Definition")]
public class IslandDefinition : ScriptableObject
{
    [Tooltip("The island's interior scene, named exactly as the scene asset is. The scene must also be in " +
             "the Build Settings scene list - Netcode refuses to load a scene that is not.")]
    [SerializeField] private string sceneName;

    [Tooltip("Index into QuestDatabase.locationNames: the island's display name in quest texts " +
             "(\"{location} yakinlarinda...\"). -1 = the island has no display name. This is the ONLY " +
             "source of that name: quest texts are written while the island is still unloaded.")]
    [SerializeField] private int locationNameIndex = -1;

    public string SceneName => sceneName != null ? sceneName : string.Empty;
    public int LocationNameIndex => locationNameIndex;

    // 0 = not checked yet, 1 = in the build list, -1 = missing. Reset on every domain reload by OnEnable,
    // so editing the build list and pressing Play re-checks.
    private int buildListCheck;

    private void OnEnable()
    {
        buildListCheck = 0;
    }

    // Forgetting to add an island scene to the Build Settings is the one setup mistake that survives the
    // move to asset references, so it gets checked out loud (see IslandTrigger) instead of surfacing as a
    // failed load once someone has swum all the way out to the island.
    public bool IsInBuildSettings()
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (buildListCheck != 0) return buildListCheck == 1;

        buildListCheck = -1;

        int sceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);

            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                buildListCheck = 1;
                break;
            }
        }

        return buildListCheck == 1;
    }
}
