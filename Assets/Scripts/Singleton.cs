using UnityEngine;

namespace Singleton
{
    public class Singleton <T> : MonoBehaviour where T : Component
    {
        private static T _sSingletonInstance;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}

