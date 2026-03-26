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
            load.allowSceneActivation = false;

            // Wait one frame so the animator has time to enter its first state.
            yield return null;

            // Hold until BOTH conditions are met:
            //   - The level is fully loaded (Unity caps progress at 0.9 until activation is allowed)
            //   - The transition animation has played to completion
            while (!(load.progress >= 0.9f && IsAnimationComplete()))
                yield return null;

            // Activating the scene destroys this scene and everything in it.
            load.allowSceneActivation = true;
        }

        /// <summary>
        /// Returns true when the named animation state has played through once.
        /// If no Animator is assigned, returns true immediately so loading is
        /// not blocked.
        /// </summary>
        private bool IsAnimationComplete()
        {
            if (transitionAnimator == null) return true;

            AnimatorStateInfo state = transitionAnimator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(animationStateName) && state.normalizedTime >= 1f;
        }
    }
}
