using System.Collections;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    // Attached to an animal GameObject at spawn. Waits for model to finish loading,
    // then applies the MC texture and removes itself.
    public class MobTextureApplier : MonoBehaviour
    {
        public string EntityClassName;

        IEnumerator Start()
        {
            // Model prefabs load on next frames after entity init
            yield return null;
            yield return null;

            var entity = GetComponent<EntityAlive>();
            MobVisuals.ApplyTexture(entity, EntityClassName);
            Destroy(this);
        }
    }
}
