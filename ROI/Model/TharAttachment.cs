using System;
using System.Collections.Generic;

namespace ROI.Model
{
    [Serializable]
    public class TharAttachment
    {
        [Serializable]
        public class ModuleType
        {
            public int Value { get; set; }
        }

        [Serializable]
        public class Type
        {
            public int Value { get; set; }
        }

        [Serializable]
        public class Item
        {
            public string Filename { get; set; }
            public ModuleType ModuleType { get; set; }
            public string RecordNo { get; set; }
            public Type Type { get; set; }
            public string Details { get; set; }
            public string Content_URL { get; set; }
            public string Content_Base64 { get; set; }

        }

        [Serializable]
        public class RootObject
        {
            public List<Item> Items { get; set; }
        }
    }
}
