using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CreateManequiController
{
    const string ModelPath = "Assets/Mariana/final/3DModel/ManequiModel.fbx";
    const string ControllerPath = "Assets/Mariana/final/3DModel/ManequiController.controller";

    [MenuItem("Tools/Animation/Create Manequi Controller")]
    static void Create()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__"))
            .ToArray();

        AnimationClip clip01 = FindClip(clips, "01", "Laser");
        AnimationClip clip02 = FindClip(clips, "02", "Orb");
        AnimationClip clip03 = FindClip(clips, "03", "Trail");

        if (clip01 == null || clip02 == null || clip03 == null)
        {
            string found = string.Join(", ", clips.Select(clip => clip.name));
            EditorUtility.DisplayDialog(
                "No se encontraron los tres clips",
                "Clips encontrados en ManequiModel.fbx: " + (found.Length == 0 ? "ninguno" : found),
                "Cerrar");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null &&
            !EditorUtility.DisplayDialog("Reemplazar Controller", "Ya existe ManequiController.controller. ¿Lo reemplazo?", "Reemplazar", "Cancelar"))
            return;

        if (AssetDatabase.LoadMainAssetAtPath(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.states.ToList().ForEach(entry => stateMachine.RemoveState(entry.state));

        AnimatorState laser = stateMachine.AddState("Laser");
        laser.motion = clip01;
        AnimatorState orb = stateMachine.AddState("Orb");
        orb.motion = clip02;
        AnimatorState trail = stateMachine.AddState("Trail");
        trail.motion = clip03;
        stateMachine.defaultState = laser;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Controller creado", "Se creó ManequiController.controller con Laser, Orb y Trail. Laser (clip 01) es el estado predeterminado.", "OK");
        Selection.activeObject = controller;
    }

    static AnimationClip FindClip(AnimationClip[] clips, string number, string effect)
    {
        return clips.FirstOrDefault(clip =>
        {
            string normalizedName = Normalize(clip.name);
            return normalizedName.Contains(number) && normalizedName.Contains(effect.ToLowerInvariant());
        });
    }

    static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
