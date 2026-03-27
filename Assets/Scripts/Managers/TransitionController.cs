using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Place this on a GameObject in your transition scene.
    /// Immediately begins loading the next level in the background while your
    /// animation plays. Once both the animation has finished AND the level is
    /// fully loaded, the new scene activates and everything in the transition
    /// scene is automatically destroyed.
    ///
    /// Setup:
    ///   1. Create a scene called (e.g.) "Transition".
    ///   2. Add your animated Yuumi and camera to it.
    ///   3. Add this script to any GameObject and assign the Animator.
    ///   4. Set Animation State Name to match the state in your Animator Controller.
    ///   5. Add the transition scene to Build Settings.
    /// </summary>
    public class TransitionController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Animator playing the transition animation.")]
        [SerializeField] private Animator transitionAnimator;

        [Header("Settings")]
        [Tooltip("Exact name of the Animator state that plays during the transition.")]
        [SerializeField] private string animationStateName = "Transition";

        private void Start()
        {
            string nextScene = LevelTransitionData.NextSceneName;

            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogError("TransitionController: LevelTransitionData.NextSceneName is not set. " +
                               "Make sure LevelManager is populating it before loading this scene.");
                return;
            }

            StartCoroutine(RunTransition(nextScene));
        }

        private IEnumerator RunTransition(string targetScene)
        {
            // Start loading the next scene immediately but keep it hidden.
            AsyncOperation load = SceneManager.LoadSceneAsync(targetScene);

            if (load == null)
            {
                Debug.LogError($"TransitionController: Failed to load scene '{targetScene}'. " +
                               "Make sure it is added to Build Settings (File → Build Settings → Add Open Scenes).");
                yield break;
            }

            load.allowSceneActivation = false;

            // Wait until the Animator has entered the named state before we
            // start checking for completion. Without this, normalizedTime reads
            // as 0 on a state that hasn't started yet and the check would pass
            // immediately, skipping the animation entirely.
            if (transitionAnimator != null)
            {
                yield return null; // one frame for Animator to initialise
                float waitTime = 0f;
                while (!transitionAnimator.GetCurrentAnimatorStateInfo(0).IsName(animationStateName))
                {
                    waitTime += Time.deltaTime;
                    if (waitTime > 2f)
                    {
                        Debug.LogWarning($"TransitionController: Animator never entered state " +
                                         $"'{animationStateName}'. Check the state name matches exactly. " +
                                         $"Proceeding without waiting for animation.");
                        break;
                    }
                    yield return null;
                }
            }

            // Latch animation completion — once normalizedTime >= 1 is observed we
            // keep animationDone = true even if the Animator transitions to another
            // state (e.g. an idle after the clip ends), which would otherwise cause
            // IsName() to return false and block scene activation forever.
            bool animationDone = transitionAnimator == null;

            // Hold until BOTH conditions are met:
            //   - The level is fully loaded (Unity caps progress at 0.9 until activation is allowed)
            //   - The transition animation has played to completion
            while (true)
            {
                if (!animationDone && transitionAnimator != null)
                {
                    AnimatorStateInfo state = transitionAnimator.GetCurrentAnimatorStateInfo(0);
                    if (state.IsName(animationStateName) && state.normalizedTime >= 1f)
                    {
                        animationDone = true;
                        Debug.Log("TransitionController: Animation complete.");
                    }
                }

                if (animationDone && load.progress >= 0.9f)
                    break;

                yield return null;
            }

            Debug.Log($"TransitionController: Activating scene '{targetScene}'.");

            // Activating the scene destroys this scene and everything in it.
            load.allowSceneActivation = true;
        }
    }
}
