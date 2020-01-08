using System;

namespace ROI.Model
{
    [Serializable]
    public class TharNote
    {
        [Serializable]
        public class ModuleType
        {
            public int Value { get; set; }
        }
        [Serializable]
        public class RootObject
        {
            public int UserId { get; set; }
            public ModuleType ModuleType { get; set; }
            public string ModuleNo { get; set; }
            public bool IsPriority { get; set; }
            public string Notes { get; set; }
        }
    }
}
