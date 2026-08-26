using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Automator
{
    /// <summary>
    /// Registry for action steps. Discovers implementations via reflection.
    /// </summary>
    public static class ActionStepRegistry
    {
        private static List<ActionStep> _steps;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets all registered action steps.
        /// </summary>
        public static List<ActionStep> Steps
        {
            get
            {
                if (_steps == null)
                {
                    lock (_lock)
                    {
                        if (_steps == null)
                        {
                            DiscoverSteps();
                        }
                    }
                }
                return _steps;
            }
        }

        /// <summary>
        /// Finds a step by its key.
        /// </summary>
        public static ActionStep GetStep(string key)
        {
            return Steps.FirstOrDefault(s => s.Key == key);
        }

        /// <summary>
        /// Forces re-discovery of all steps.
        /// </summary>
        public static void Refresh()
        {
            lock (_lock)
            {
                DiscoverSteps();
            }
        }

        private static void DiscoverSteps()
        {
            _steps = new List<ActionStep>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<ActionStep>())
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }

                try
                {
                    ActionStep instance = (ActionStep)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(instance.Key))
                    {
                        _steps.Add(instance);
                    }
                }
                catch
                {
                    // Skip types that fail to instantiate.
                }
            }

            // Sort by category then name for consistent ordering
            _steps = _steps.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
        }
    }
}
