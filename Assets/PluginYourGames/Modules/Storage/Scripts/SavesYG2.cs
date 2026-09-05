using System;
using System.Collections.Generic;

namespace YG
{
    [Serializable]
    public class AstroSaveEntry
    {
        public string key;
        public int intValue;
        public string strValue;
        public bool isString;
    }

    [System.Serializable]
    public partial class SavesYG
    {
        public int idSave;

        /// <summary>AstroDrift: универсальное хранилище ISaveService (key → int/string).
        /// JSON YG2 не сериализует Dictionary — список пар.</summary>
        public List<AstroSaveEntry> astro = new List<AstroSaveEntry>();
    }
}
