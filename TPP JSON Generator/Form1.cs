using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace TPP_JSON
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            #region Initialization
            List<Move> ms = new List<Move>();
            for (int i = 0; i < gen5moves.Length; i++)
            {
                string[] m5 = gen5moves[i].Split(',');
                string s = m5[0];
                int ind = movedata.ToList().FindIndex(m => (m.Split(',')[0] == s));
                string[] m6 = movedata[ind].Split(',');
                for (int j = 0; j < m5.Length; j++)
                {
                    if (m5[j] != "undefined")
                        m6[j] = m5[j];
                }
                movedata[ind] = string.Join(",", m6);
            }
            for (int i = 0; i < gen4moves.Length; i++)
            {
                string[] m4 = gen4moves[i].Split(',');
                string s = m4[0];
                int ind = movedata.ToList().FindIndex(m => (m.Split(',')[0] == s));
                string[] m6 = movedata[ind].Split(',');
                for (int j = 0; j < m4.Length; j++)
                {
                    if (m4[j] != "undefined")
                        m6[j] = m4[j];
                }
                movedata[ind] = string.Join(",", m6);
            }
            foreach (string s in movedata)
            {
                Move m = new Move(s);
                if (m.id != -1)
                    ms.Add(m);
            }
            ms = ms.OrderBy(m => m.id).ToList();
            moves = ms.ToArray();
            Species = new Dictionary<ushort, string>();
            for (int i = 0; i < specvals.Length; i++)
            {
                Species.Add((ushort)specvals[i], specieslist[i]);
            }
            #endregion
        }

        public Dictionary<ushort, string> Species;
        public Move[] moves;

        private void B_Go_Click(object sender, EventArgs e)
        {
            B_Go.Enabled = B_Open.Enabled = false;
            PB_Progress.Maximum = 540;
            PB_Progress.Minimum = 0;
            PB_Progress.Value = 0;
            Thread thread = new Thread(() => 
            { 
                this.Generate();
                B_Go.Invoke(new Action(() => { B_Go.Enabled = true; }));
                B_Open.Invoke(new Action(() => { B_Open.Enabled = true; }));
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void Generate()
        {
            byte[] save = File.ReadAllBytes(TB_In.Text);
            uint save_a = BitConverter.ToUInt32(save, 0xCF1C);
            uint save_b = BitConverter.ToUInt32(save, 0x4CF1C);
            List<string> sav;
            if (save_a > save_b)
            {
                sav = BuildJSON(save.Take(0x40000).ToArray());
            }
            else
            {
                sav = BuildJSON(save.Skip(0x40000).Take(0x40000).ToArray());
            }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = "out.json";
            bool ok = false;
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ok = (sfd.ShowDialog() == DialogResult.OK)));
            }
            else
            {
                ok = (sfd.ShowDialog() == DialogResult.OK);
            }
            if (ok)
                File.WriteAllLines(sfd.FileName, sav);

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => B_Go.Enabled = B_Open.Enabled = true));
            }
            else
            {
                B_Go.Enabled = B_Open.Enabled = true;
            }
        }

        private void Advance_ProgressBar(ProgressBar PB)
        {
            if (PB.InvokeRequired)
                PB.Invoke(new Action(() => PB.PerformStep()));
            else
                PB.PerformStep();
        }

        private List<string> BuildJSON(byte[] save)
        {
            int boxoffset = 0xCF30;
            int monlen = 136;
            List<string> lines = new List<string>();
            Shinies = new Tuple<int,bool>[540];
            shones = new bool[494];
            for (int i = 0; i < 540; i++)
            {
                byte[] decmon = DecryptMon(save.Skip(boxoffset + monlen * i).Take(monlen).ToArray());
                Shinies[i] = Tuple.Create((int)BitConverter.ToUInt16(decmon, 0x8), isShiny(decmon));
                if (isShiny(decmon))
                {
                    shones[(int)BitConverter.ToUInt16(decmon, 0x8)] = isShiny(decmon);
                }
            }
            lines.Add("[");
            for (int i = 0; i < 540; i++)
            {
                BuildMon(lines, save.Skip(boxoffset + monlen * i).Take(monlen).ToArray(), i);
                Advance_ProgressBar(PB_Progress);
            }
            lines.Add("]");
            return lines;
        }

        Tuple<int, bool>[] Shinies;
        bool[] shones;

        private void BuildMon(List<string> lines, byte[] mon, int position)
        {
            mon = DecryptMon(mon);
            lines.Add("    {");
            lines.Add(GetLine("ability", abilities[mon[0x15]], 2, true));
            lines.Add(GetLine("dexNumber", BitConverter.ToUInt16(mon, 0x8), 2, true));
            lines.Add(Tabs(2) + GetKey("enabled") + ": true,");
            if (CHK_TPPArceusFix.Checked)
            {
                int item = BitConverter.ToInt16(mon, 0xA);
                if (BitConverter.ToUInt16(mon, 0x8) == 493 && item > 0x129 && item < 0x13A)
                {
                    int[] PlateToType = new int[] { 9, 10, 12, 11, 14, 1, 3, 4, 2, 13, 6, 5, 7, 15, 16, 8 };
                    lines.Add(GetLine("form", (PlateToType[item - 0x12A]), 2, true));
                }
                else
                    lines.Add(GetLine("form", (mon[0x40] >> 3), 2, true));
            }
            else
            {
                lines.Add(GetLine("form", (mon[0x40] >> 3), 2, true));
            }     
            string gender = null;
            switch ((mon[0x40] >> 1) & 3)
            {
                case 0:
                    gender = "m";
                    break;
                case 1:
                    gender = "f";
                    break;
                case 2:
                case 3:
                    gender = null;
                    break;
            }
            lines.Add(GetLine("gender", gender, 2, true));
            lines.Add(GetLine("item", items[BitConverter.ToUInt16(mon, 0x0A)], 2, true));
            lines.Add(GetLine("iv", ((mon[0x38] << 0) & 0x1F), 2, true));
            lines.Add(Tabs(2) + GetKey("moves") + ": [");
            for (int i = 0; i < 4; i++)
            {
                string[] directions = new string[] { "up", "right", "left", "down" };
                ushort move = BitConverter.ToUInt16(mon, 0x28 + i * 2);
                ushort nextmove = BitConverter.ToUInt16(mon, 0x28 + (i + 1) * 2);
                if (move == 0)
                    continue;
                lines.Add(Tabs(3) + "{");
                Move mov = moves[moves.ToList().FindIndex(m => m.id == (int)move)];
                lines.Add(Tabs(4) + GetKey("accuracy") + ": " + (mov.accuracy) + ",");
                lines.Add(GetLine("category", mov.category, 4, true));
                lines.Add(GetLine("direction", directions[i], 4, true));
                lines.Add(GetLine("name", mov.name, 4, true));
                lines.Add(Tabs(4) + GetKey("power") + ": " + mov.power + ",");
                lines.Add(Tabs(4) + GetKey("pp") + ": " + mov.pp + ",");
                lines.Add(GetLine("type", mov.type, 4, false));
                lines.Add(Tabs(3) + "}" + ((i < 3 && nextmove != 0) ? "," : String.Empty));
            }
            #region nickname
            /*
            DataTable CT45 = Char4to5();
            byte[] nicknamestr = new byte[24];
            nicknamestr[22] = nicknamestr[23] = 0xFF;
            for (int j = 0; j < 24; j += 2)
            {
                int val = BitConverter.ToUInt16(mon, 0x48 + j);
                if (val == 0xFFFF)   // If given character is a terminator, stop conversion.
                    break;

                // find entry
                int newval = (int)CT45.Rows.Find(val)[1];
                Array.Copy(BitConverter.GetBytes(newval), 0, nicknamestr, j, 2);
            }
            string nickname = "";
            for (int j = 0; j < 24; j += 2)
            {
                if ((mon[0x48 + j] == 0xFF) && mon[0x48 + j + 1] == 0xFF)   // If given character is a terminator, stop copying. There are no trash bytes or terminators in Gen 6!
                    break;
                nickname += (char)(BitConverter.ToUInt16(nicknamestr, j));
            }
            lines.Add(GetLine("name", nickname, 3, true));*/
            #endregion
            lines.Add(Tabs(2) + "],");
            lines.Add(GetLine("name", GetName(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F), BitConverter.ToUInt16(mon, 0xA), isShiny(mon)), 2, true));
            uint nature = BitConverter.ToUInt32(mon, 0) % 0x19;
            lines.Add(GetLine("nature", natures[nature], 2, true));
            lines.Add(GetLine("position", position, 2, true));
            string ss = GetShinyString(BitConverter.ToUInt16(mon, 0x8), position);
            lines.Add(Tabs(2) + GetKey("shiny") + ": " + ss + ",");
            if (ss == "false")
            {
                int spec = BitConverter.ToUInt16(mon, 0x8);
                int sposition = Shinies.ToList().FindIndex(t => (t.Item1 == spec && t.Item2));
                lines.Add(GetLine("sposition", sposition, 2, true));
            }
            lines.Add(Tabs(2) + GetKey("stats") + ": {");
            byte[] evs = new byte[6];
            evs[0] = mon[0x18];
            evs[1] = mon[0x19];
            evs[2] = mon[0x1A];
            evs[3] = mon[0x1C];
            evs[4] = mon[0x1D];
            evs[5] = mon[0x1B];
            string[] stats = new string[] { "hp", "atk", "def", "spa", "spd", "spe" };
            uint iv = BitConverter.ToUInt32(mon, 0x38);
            byte[] ivs = new byte[6];
            for (int i = 0; i < ivs.Length; i++)
            {
                ivs[i] = (byte)((iv >> (i * 5)) & 0x1f);
            }
            byte swap = ivs[3]; //Swap = speed
            ivs[3] = ivs[4]; //speed = Spa
            ivs[4] = ivs[5]; //Spa = spd
            ivs[5] = swap; //spd = speed
            uint exp = BitConverter.ToUInt32(mon,0x10);
            byte level = GetLevel(BitConverter.ToInt16(mon, 0x08), exp);
            int[] swaps = { 0, 1, 2, 5, 3, 4 };
            int incr = swaps[(int)(nature / 5 + 1)];
            int decr = swaps[(int)(nature % 5 + 1)];
            for (int i = 0; i < stats.Length; i++)
            {
                int st = 0;
                if (i == 0)
                {
                    if (GetBaseStats(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F))[i] == 1)
                        st = 1;
                    else
                        st = (((ivs[i] + 2 * GetBaseStats(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F))[i] + evs[i] / 4 + 100) * level) / 100) + 10;
                }
                else
                {
                    st = (((ivs[i] + 2 * GetBaseStats(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F))[i] + evs[i] / 4) * level) / 100) + 5;
                    if (incr == decr)
                    {

                    }
                    else if (i == incr)
                    {
                        st *= 11; st /= 10;
                    }
                    else if (i == decr)
                    {
                        st *= 9; st /= 10;
                    }
                }
                lines.Add(GetLine(stats[i], st, 3, i < stats.Length - 1));
            }
            lines.Add(Tabs(2) + "},");
            lines.Add(Tabs(2) + GetKey("types") + ": [");
            for (int i = 0; i < GetTypings(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F)).Length; i++)
            {
                string line = GetKey(GetTypings(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F))[i]);
                if (i < GetTypings(BitConverter.ToUInt16(mon, 0x8), (mon[0x40] >> 3 & 0x1F)).Length - 1)
                    line += ",";
                lines.Add(Tabs(3) + line);
            }
            lines.Add(Tabs(2) + "]");
            lines.Add("    }" + (position < 539 ? "," : String.Empty));
        }

        private byte GetLevel(int species, uint exp)
        {
            if (exp == 0) { return 1; }
            int tl = 1; // Initial Level

            DataTable spectable = SpeciesTable();
            DataTable table = ExpTable();

            int growth = (int)spectable.Rows[species][1];

            if ((uint)table.Rows[tl][growth + 1] < exp)
            {
                while ((uint)table.Rows[tl][growth + 1] < exp)
                {
                    // While EXP for guessed level is below our current exp
                    tl += 1;
                    if (tl == 100)
                    {
                        exp = getEXP(100, species);
                        return (byte)tl;
                    }
                    // when calcexp exceeds our exp, we exit loop
                }
                if ((uint)table.Rows[tl][growth + 1] == exp) // Matches level threshold
                    return (byte)tl;
                else return (byte)(tl - 1);
            }
            else return (byte)tl;
        }

        public static uint getEXP(int level, int species)
        {
            // Fetch Growth
            if ((level == 0) || (level == 1))
                return 0;
            if (level > 100) level = 100;

            DataTable spectable = SpeciesTable();
            int growth = (int)spectable.Rows[species][1];

            uint exp = (uint)ExpTable().Rows[level][growth + 1];
            return exp;
        }

        private string GetShinyString(int species, int position)
        {
            if (!shones[species])
                return "null";
            return Shinies[position].Item2.ToString().ToLower();
        }

        private string GetName(int species, int form, int item, bool isShiny)
        {
            string name;
            if (species == 493 && item > 0x129 && item < 0x13A) //Arceus with plate
            {
                string[] Plates = new string[] { "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel" };
                name = "Arceus " + Plates[item - 0x12A];
            }
            else
            {
                ushort sp = (ushort)(((form) << 11) | (species << 1));
                name = Species[sp];
            }
            if (isShiny)
                name += " (Shiny)";
            return name;
        }

        private bool isShiny(byte[] mon)
        {
            ushort TID = BitConverter.ToUInt16(mon, 0x0C);
            ushort SID = BitConverter.ToUInt16(mon, 0x0E);
            ushort LID = BitConverter.ToUInt16(mon, 0x0);
            ushort HID = BitConverter.ToUInt16(mon, 0x2);
            int XOR = TID ^ SID ^ LID ^ HID;
            return XOR < 8;
        }

        private byte[] DecryptMon(byte[] mon)
        {
            byte[] pkm = new byte[mon.Length];
            Array.Copy(mon, pkm, mon.Length);
            uint pv = BitConverter.ToUInt32(mon, 0);
            uint sv = (((pv & 0x3E000) >> 0xD) % 24);
            uint seed = BitConverter.ToUInt16(mon, 6);
            for (int i = 0; i < 64; i++)
            {
                seed = lcg(seed);
                ushort random = (ushort)((seed >> 16) & 0xFFFF);
                ushort value = BitConverter.ToUInt16(mon, 8 + i * 2);
                Array.Copy(BitConverter.GetBytes((ushort)(value ^ random)), 0, pkm, 8 + i * 2, 2);
            }
            pkm = ShuffleArray(pkm, sv);
            return pkm;
        }

        private uint lcg(uint seed)
        {
            return 0x41C64E6D * seed + 0x6073;
        }

        private byte[] ShuffleArray(byte[] pkm, uint sv)
        {
            byte[] ekx = new byte[136];
            Array.Copy(pkm, ekx, 8);

            // Now to shuffle the blocks

            // Define Shuffle Order Structure
            byte[] aloc = { 0, 0, 0, 0, 0, 0, 1, 1, 2, 3, 2, 3, 1, 1, 2, 3, 2, 3, 1, 1, 2, 3, 2, 3 };
            byte[] bloc = { 1, 1, 2, 3, 2, 3, 0, 0, 0, 0, 0, 0, 2, 3, 1, 1, 3, 2, 2, 3, 1, 1, 3, 2 };
            byte[] cloc = { 2, 3, 1, 1, 3, 2, 2, 3, 1, 1, 3, 2, 0, 0, 0, 0, 0, 0, 3, 2, 3, 2, 1, 1 };
            byte[] dloc = { 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 0, 0, 0, 0, 0, 0 };

            // Get Shuffle Order
            byte[] shlog = { aloc[sv], bloc[sv], cloc[sv], dloc[sv] };
            for (int b = 0; b < 4; b++)
                Array.Copy(pkm, 8 + 32 * shlog[b], ekx, 8 + 32 * b, 32);

            return ekx;
        }

        private string GetLine(string key1, string key2, int tabs, bool more)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tabs; i++)
                sb.Append("    ");
            return sb.ToString() + GetKey(key1) + ": " + GetKey(key2) + (more ? "," : String.Empty);
        }

        private string GetLine(string key1, int key2, int tabs, bool more)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tabs; i++)
                sb.Append("    ");
            return sb.ToString() + GetKey(key1) + ": " + key2 + (more ? "," : String.Empty);
        }

        private string GetKey(string key)
        {
            if (key == null)
            {
                return "null";
            }
            else
            {
                return "\"" + key + "\"";
            }
        }

        private string Tabs(int tabs)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tabs; i++)
                sb.Append("    ");
            return sb.ToString();
        }

        private string[] GetTypings(int species, int form)
        {
            if (form == 0)
            {
                return types[species];
            }
            else if (species == 351)
            {
                switch (form)
                {
                    case 1:
                        return new string[] { "Fire" };
                    case 2:
                        return new string[] { "Water" };
                    case 3:
                        return new string[] { "Ice" };
                    default:
                        return types[species];
                }
            }
            else if (species == 413)
            {
                switch (form)
                {
                    case 1:
                        return new string[] { "Bug", "Ground" };
                    case 2:
                        return new string[] { "Bug", "Steel" };
                    default:
                        return types[species];
                }
            }
            else if (species == 479)
            {
                switch (form)
                {
                    case 1:
                        return new string[] { "Electric", "Fire" };
                    case 2:
                        return new string[] { "Electric", "Water" };
                    case 3:
                        return new string[] { "Electric", "Ice" };
                    case 4:
                        return new string[] { "Electric", "Flying" };
                    case 5:
                        return new string[] { "Electric", "Grass" };
                    default:
                        return types[species];
                }
            }
            else if (species == 492)
            {
                if (form == 1)
                    return new string[] { "Grass", "Flying" };
                else
                    return types[species];
            }
            else if (species == 493)
            {
                return new string[] { typings[form] };
            }
            else
            {
                return types[species];
            }
        }

        public string[] typings = {
                "Normal",
                "Fighting",
                "Flying", 
                "Poison", 
                "Ground", 
                "Rock", 
                "Bug", 
                "Ghost",
                "Steel",
                "Fire", 
                "Water",
                "Grass",
                "Electric",
                "Psychic",
                "Ice",
                "Dragon",
                "Dark",
                "Fairy"
                    };

        private string[] natures = {"Hardy",
"Lonely",
"Brave",
"Adamant",
"Naughty",
"Bold",
"Docile",
"Relaxed",
"Impish",
"Lax",
"Timid",
"Hasty",
"Serious",
"Jolly",
"Naive",
"Modest",
"Mild",
"Quiet",
"Bashful",
"Rash",
"Calm",
"Gentle",
"Sassy",
"Careful",
"Quirky"};

        private int[] GetBaseStats(int species, int form)
        {
            if (form == 0)
            {
                return basestats[species];
            }
            else if (species == 386)
            {
                switch (form)
                {
                    case 1:
                        return new int[] { 50, 180, 20, 180, 20, 150 };
                    case 2:
                        return new int[] { 50, 70, 160, 70, 160, 90 };
                    case 3:
                        return new int[] { 50, 95, 90, 95, 90, 180 };
                    default:
                        return new int[] { 0, 0, 0, 0, 0, 0 };

                }
            }
            else if (species == 413)
            {
                switch (form)
                {
                    case 1:
                        return new int[] { 60, 79, 105, 59, 85, 36 };
                    case 2:
                        return new int[] { 60, 69, 95, 69, 95, 36 };
                    default:
                        return new int[] { 0, 0, 0, 0, 0, 0 };
                }
            }
            else if (species == 479)
            {
                return new int[] { 50, 65, 107, 105, 107, 86 };
            }
            else if (species == 487)
            {
                return new int[] { 150, 120, 100, 120, 100, 90 };
            }
            else if (species == 492)
            {
                return new int[] { 100, 103, 75, 120, 75, 127 };
            }
            else
            {
                return basestats[species];
            }
        }

        private string[] abilities = new string[]{"—",
"Stench",
"Drizzle",
"Speed Boost",
"Battle Armor",
"Sturdy",
"Damp",
"Limber",
"Sand Veil",
"Static",
"Volt Absorb",
"Water Absorb",
"Oblivious",
"Cloud Nine",
"Compound Eyes",
"Insomnia",
"Color Change",
"Immunity",
"Flash Fire",
"Shield Dust",
"Own Tempo",
"Suction Cups",
"Intimidate",
"Shadow Tag",
"Rough Skin",
"Wonder Guard",
"Levitate",
"Effect Spore",
"Synchronize",
"Clear Body",
"Natural Cure",
"Lightning Rod",
"Serene Grace",
"Swift Swim",
"Chlorophyll",
"Illuminate",
"Trace",
"Huge Power",
"Poison Point",
"Inner Focus",
"Magma Armor",
"Water Veil",
"Magnet Pull",
"Soundproof",
"Rain Dish",
"Sand Stream",
"Pressure",
"Thick Fat",
"Early Bird",
"Flame Body",
"Run Away",
"Keen Eye",
"Hyper Cutter",
"Pickup",
"Truant",
"Hustle",
"Cute Charm",
"Plus",
"Minus",
"Forecast",
"Sticky Hold",
"Shed Skin",
"Guts",
"Marvel Scale",
"Liquid Ooze",
"Overgrow",
"Blaze",
"Torrent",
"Swarm",
"Rock Head",
"Drought",
"Arena Trap",
"Vital Spirit",
"White Smoke",
"Pure Power",
"Shell Armor",
"Air Lock",
"Tangled Feet",
"Motor Drive",
"Rivalry",
"Steadfast",
"Snow Cloak",
"Gluttony",
"Anger Point",
"Unburden",
"Heatproof",
"Simple",
"Dry Skin",
"Download",
"Iron Fist",
"Poison Heal",
"Adaptability",
"Skill Link",
"Hydration",
"Solar Power",
"Quick Feet",
"Normalize",
"Sniper",
"Magic Guard",
"No Guard",
"Stall",
"Technician",
"Leaf Guard",
"Klutz",
"Mold Breaker",
"Super Luck",
"Aftermath",
"Anticipation",
"Forewarn",
"Unaware",
"Tinted Lens",
"Filter",
"Slow Start",
"Scrappy",
"Storm Drain",
"Ice Body",
"Solid Rock",
"Snow Warning",
"Honey Gather",
"Frisk",
"Reckless",
"Multitype",
"Flower Gift",
"Bad Dreams",
"Pickpocket",
"Sheer Force",
"Contrary",
"Unnerve",
"Defiant",
"Defeatist",
"Cursed Body",
"Healer",
"Friend Guard",
"Weak Armor",
"Heavy Metal",
"Light Metal",
"Multiscale",
"Toxic Boost",
"Flare Boost",
"Harvest",
"Telepathy",
"Moody",
"Overcoat",
"Poison Touch",
"Regenerator",
"Big Pecks",
"Sand Rush",
"Wonder Skin",
"Analytic",
"Illusion",
"Imposter",
"Infiltrator",
"Mummy",
"Moxie",
"Justified",
"Rattled",
"Magic Bounce",
"Sap Sipper",
"Prankster",
"Sand Force",
"Iron Barbs",
"Zen Mode",
"Victory Star",
"Turboblaze",
"Teravolt",
"Aroma Veil",
"Flower Veil",
"Cheek Pouch",
"Protean",
"Fur Coat",
"Magician",
"Bulletproof",
"Competitive",
"Strong Jaw",
"Refrigerate",
"Sweet Veil",
"Stance Change",
"Gale Wings",
"Mega Launcher",
"Grass Pelt",
"Symbiosis",
"Tough Claws",
"Pixilate",
"Gooey",
"Aerilate",
"Parental Bond",
"Dark Aura",
"Fairy Aura",
"Aura Break",
"Primordial Sea",
"Desolate Land",
"Delta Stream"};

        string[] gen4moves = new string[]{"acupressure,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"assist,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"aquaring,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"beatup,undefined,undefined,undefined,undefined,10,undefined,undefined",
"bide,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"bind,undefined,undefined,75,undefined,undefined,undefined,undefined",
"bonerush,undefined,undefined,80,undefined,undefined,undefined,undefined",
"brickbreak,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"bulletseed,undefined,undefined,undefined,undefined,10,undefined,undefined",
"chatter,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"clamp,undefined,undefined,75,undefined,undefined,10,undefined",
"conversion,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"copycat,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"cottonspore,undefined,undefined,85,undefined,undefined,undefined,undefined",
"covet,undefined,undefined,undefined,undefined,40,undefined,undefined",
"crabhammer,undefined,undefined,85,undefined,undefined,undefined,undefined",
"crushgrip,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"curse,undefined,undefined,undefined,undefined,undefined,undefined,???",
"defog,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"detect,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"disable,undefined,undefined,80,undefined,undefined,undefined,undefined",
"doomdesire,undefined,undefined,85,undefined,120,undefined,undefined",
"drainpunch,undefined,undefined,undefined,undefined,60,5,undefined",
"dreameater,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"embargo,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"encore,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"endeavor,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"explosion,undefined,undefined,undefined,undefined,500,undefined,undefined",
"extremespeed,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"fakeout,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"feint,undefined,undefined,undefined,undefined,50,undefined,undefined",
"firespin,undefined,undefined,70,undefined,15,undefined,undefined",
"flail,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"focuspunch,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"foresight,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"furycutter,undefined,undefined,undefined,undefined,10,undefined,undefined",
"futuresight,undefined,undefined,90,undefined,80,15,undefined",
"gigadrain,undefined,undefined,undefined,undefined,60,undefined,undefined",
"glare,undefined,undefined,75,undefined,undefined,undefined,undefined",
"growth,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"healblock,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"healingwish,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"hiddenpower,undefined,undefined,undefined,undefined,0,undefined,undefined",
"hiddenpowerbug,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerdark,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerdragon,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerelectric,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerfighting,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerfire,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerflying,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerghost,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowergrass,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerground,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerice,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerpoison,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerpsychic,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerrock,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowersteel,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerwater,undefined,undefined,undefined,undefined,70,undefined,undefined",
"highjumpkick,undefined,undefined,undefined,undefined,100,20,undefined",
"iciclespear,undefined,undefined,undefined,undefined,10,undefined,undefined",
"imprison,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"jumpkick,undefined,undefined,undefined,undefined,85,25,undefined",
"lastresort,undefined,undefined,undefined,undefined,130,undefined,undefined",
"luckychant,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"lunardance,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"magiccoat,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"magmastorm,undefined,undefined,70,undefined,undefined,undefined,undefined",
"magnetrise,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"metronome,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"mimic,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"minimize,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"miracleeye,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"mirrormove,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"moonlight,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"morningsun,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"odorsleuth,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"outrage,undefined,undefined,undefined,undefined,undefined,15,undefined",
"payback,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"petaldance,undefined,undefined,undefined,undefined,90,20,undefined",
"poisongas,undefined,undefined,55,undefined,undefined,undefined,undefined",
"powertrick,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"protect,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"psychup,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"recycle,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"reversal,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"roar,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"rockblast,undefined,undefined,80,undefined,undefined,undefined,undefined",
"sandtomb,undefined,undefined,70,undefined,15,undefined,undefined",
"scaryface,undefined,undefined,90,undefined,undefined,undefined,undefined",
"selfdestruct,undefined,undefined,undefined,undefined,400,undefined,undefined",
"sketch,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"skillswap,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"spikes,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"spite,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"stealthrock,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"suckerpunch,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"synthesis,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"tackle,undefined,undefined,95,undefined,35,undefined,undefined",
"tailglow,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"tailwind,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"taunt,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"thrash,undefined,undefined,undefined,undefined,90,20,undefined",
"torment,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"toxic,undefined,undefined,85,undefined,undefined,undefined,undefined",
"toxicspikes,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"transform,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"uproar,undefined,undefined,undefined,undefined,50,undefined,undefined",
"whirlpool,undefined,undefined,70,undefined,15,undefined,undefined",
"whirlwind,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"wish,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"worryseed,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"wrap,undefined,undefined,85,undefined,undefined,undefined,undefined",
"wringout,undefined,undefined,undefined,undefined,undefined,undefined,undefined"};

        string[] gen5moves = new string[]{"acidarmor,undefined,undefined,undefined,undefined,undefined,40,undefined",
"aircutter,undefined,undefined,undefined,undefined,55,undefined,undefined",
"airslash,undefined,undefined,undefined,undefined,undefined,20,undefined",
"aromatherapy,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"assist,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"assurance,undefined,undefined,undefined,undefined,50,undefined,undefined",
"aurasphere,undefined,undefined,undefined,undefined,90,undefined,undefined",
"barrier,undefined,undefined,undefined,undefined,undefined,30,undefined",
"bestow,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"bind,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"blizzard,undefined,undefined,undefined,undefined,120,undefined,undefined",
"block,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"bubble,undefined,undefined,undefined,undefined,20,undefined,undefined",
"bugbuzz,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"camouflage,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"charm,undefined,undefined,undefined,undefined,undefined,undefined,Normal",
"chatter,undefined,undefined,undefined,undefined,60,undefined,undefined",
"clamp,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"conversion,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"copycat,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"cottonspore,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"covet,undefined,undefined,undefined,undefined,undefined,40,undefined",
"crabhammer,undefined,undefined,undefined,undefined,90,undefined,undefined",
"defog,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"dracometeor,undefined,undefined,undefined,undefined,140,undefined,undefined",
"dragonpulse,undefined,undefined,undefined,undefined,90,undefined,undefined",
"echoedvoice,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"energyball,undefined,undefined,undefined,undefined,80,undefined,undefined",
"extrasensory,undefined,undefined,undefined,undefined,undefined,30,undefined",
"finalgambit,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"fireblast,undefined,undefined,undefined,undefined,120,undefined,undefined",
"firepledge,undefined,undefined,undefined,undefined,50,undefined,undefined",
"firespin,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"flamethrower,undefined,undefined,undefined,undefined,95,undefined,undefined",
"followme,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"frostbreath,undefined,undefined,undefined,undefined,40,undefined,undefined",
"furycutter,undefined,undefined,undefined,undefined,20,undefined,undefined",
"futuresight,undefined,undefined,undefined,undefined,100,undefined,undefined",
"glare,undefined,undefined,90,undefined,undefined,undefined,undefined",
"grasswhistle,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"grasspledge,undefined,undefined,undefined,undefined,50,undefined,undefined",
"growl,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"growth,undefined,undefined,undefined,undefined,undefined,40,undefined",
"gunkshot,undefined,undefined,70,undefined,undefined,undefined,undefined",
"healbell,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"healblock,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"healpulse,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"heatwave,undefined,undefined,undefined,undefined,100,undefined,undefined",
"hex,undefined,undefined,undefined,undefined,50,undefined,undefined",
"hiddenpower,undefined,undefined,undefined,undefined,0,undefined,undefined",
"hiddenpowerbug,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerdark,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerdragon,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerelectric,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerfighting,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerfire,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerflying,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerghost,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowergrass,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerground,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerice,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerpoison,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerpsychic,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerrock,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowersteel,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hiddenpowerwater,undefined,undefined,undefined,undefined,70,undefined,undefined",
"hurricane,undefined,undefined,undefined,undefined,120,undefined,undefined",
"hydropump,undefined,undefined,undefined,undefined,120,undefined,undefined",
"hypervoice,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"icebeam,undefined,undefined,undefined,undefined,95,undefined,undefined",
"incinerate,undefined,undefined,undefined,undefined,30,undefined,undefined",
"knockoff,undefined,undefined,undefined,undefined,20,undefined,undefined",
"leafstorm,undefined,undefined,undefined,undefined,140,undefined,undefined",
"lick,undefined,undefined,undefined,undefined,20,undefined,undefined",
"lowsweep,undefined,undefined,undefined,undefined,60,undefined,undefined",
"magicroom,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"magmastorm,undefined,undefined,undefined,undefined,120,undefined,undefined",
"meanlook,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"metalsound,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"meteormash,undefined,undefined,85,undefined,100,undefined,undefined",
"metronome,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"minimize,undefined,undefined,undefined,undefined,undefined,20,undefined",
"moonlight,undefined,undefined,undefined,undefined,undefined,undefined,Normal",
"mudsport,Status,300,true,Mud Sport,0,15,Ground",
"muddywater,undefined,undefined,undefined,undefined,95,undefined,undefined",
"naturepower,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"overheat,undefined,undefined,undefined,undefined,140,undefined,undefined",
"perishsong,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"pinmissile,undefined,undefined,85,undefined,14,undefined,undefined",
"poisonfang,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"poisongas,undefined,undefined,80,undefined,undefined,undefined,undefined",
"poisonpowder,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"powergem,undefined,undefined,undefined,undefined,70,undefined,undefined",
"psychoshift,undefined,undefined,90,undefined,undefined,undefined,undefined",
"psywave,undefined,undefined,80,undefined,undefined,undefined,undefined",
"quickguard,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"ragepowder,Status,476,true,Rage Powder,0,20,Bug",
"relicsong,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"roar,undefined,undefined,100,undefined,undefined,undefined,undefined",
"rocktomb,undefined,undefined,80,undefined,50,10,undefined",
"round,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"sandtomb,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"sacredsword,undefined,undefined,undefined,undefined,undefined,20,undefined",
"scald,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"screech,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"secretpower,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"sing,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"skillswap,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"skullbash,undefined,undefined,undefined,undefined,100,15,undefined",
"skydrop,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"sleeppowder,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"smellingsalts,undefined,undefined,undefined,undefined,60,undefined,undefined",
"smog,undefined,undefined,undefined,undefined,20,undefined,undefined",
"snarl,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"snore,undefined,undefined,undefined,undefined,40,undefined,undefined",
"spore,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"stormthrow,undefined,undefined,undefined,undefined,40,undefined,undefined",
"stringshot,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"strugglebug,undefined,undefined,undefined,undefined,30,undefined,undefined",
"stunspore,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"substitute,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"submission,undefined,undefined,undefined,undefined,undefined,25,undefined",
"supersonic,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"surf,undefined,undefined,undefined,undefined,95,undefined,undefined",
"sweetkiss,undefined,undefined,undefined,undefined,undefined,undefined,Normal",
"sweetscent,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"swordsdance,undefined,undefined,undefined,undefined,undefined,30,undefined",
"synchronoise,undefined,undefined,undefined,undefined,70,15,undefined",
"tailwind,undefined,undefined,undefined,undefined,undefined,30,undefined",
"technoblast,undefined,undefined,undefined,undefined,85,undefined,undefined",
"thief,undefined,undefined,undefined,undefined,40,10,undefined",
"thunder,undefined,undefined,undefined,undefined,120,undefined,undefined",
"thunderbolt,undefined,undefined,undefined,undefined,95,undefined,undefined",
"uproar,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"toxic,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"vinewhip,undefined,undefined,undefined,undefined,35,15,undefined",
"wakeupslap,undefined,undefined,undefined,undefined,60,undefined,undefined",
"waterpledge,undefined,undefined,undefined,undefined,50,undefined,undefined",
"watersport,Status,346,true,Water Sport,0,15,Water",
"whirlwind,undefined,undefined,100,undefined,undefined,undefined,undefined",
"wideguard,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"whirlpool,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"willowisp,undefined,undefined,75,undefined,undefined,undefined,undefined",
"wonderroom,undefined,undefined,undefined,undefined,undefined,undefined,undefined",
"wrap,undefined,undefined,undefined,undefined,undefined,undefined,undefined"};

        string[] movedata = new string[]{"absorb,Special,71,100,Absorb,20,25,Grass",
"acid,Special,51,100,Acid,40,30,Poison",
"acidarmor,Status,151,true,Acid Armor,0,20,Poison",
"acidspray,Special,491,100,Acid Spray,40,20,Poison",
"acrobatics,Physical,512,100,Acrobatics,55,15,Flying",
"acupressure,Status,367,true,Acupressure,0,30,Normal",
"aerialace,Physical,332,true,Aerial Ace,60,20,Flying",
"aeroblast,Special,177,95,Aeroblast,100,5,Flying",
"afteryou,Status,495,true,After You,0,15,Normal",
"agility,Status,97,true,Agility,0,30,Psychic",
"aircutter,Special,314,95,Air Cutter,60,25,Flying",
"airslash,Special,403,95,Air Slash,75,15,Flying",
"allyswitch,Status,502,true,Ally Switch,0,15,Psychic",
"amnesia,Status,133,true,Amnesia,0,20,Psychic",
"ancientpower,Special,246,100,Ancient Power,60,5,Rock",
"aquajet,Physical,453,100,Aqua Jet,40,20,Water",
"aquaring,Status,392,true,Aqua Ring,0,20,Water",
"aquatail,Physical,401,90,Aqua Tail,90,10,Water",
"armthrust,Physical,292,100,Arm Thrust,15,20,Fighting",
"aromatherapy,Status,312,true,Aromatherapy,0,5,Grass",
"aromaticmist,Status,597,true,Aromatic Mist,0,20,Fairy",
"assist,Status,274,true,Assist,0,20,Normal",
"assurance,Physical,372,100,Assurance,60,10,Dark",
"astonish,Physical,310,100,Astonish,30,15,Ghost",
"attackorder,Physical,454,100,Attack Order,90,15,Bug",
"attract,Status,213,100,Attract,0,15,Normal",
"aurasphere,Special,396,true,Aura Sphere,80,20,Fighting",
"aurorabeam,Special,62,100,Aurora Beam,65,20,Ice",
"autotomize,Status,475,true,Autotomize,0,15,Steel",
"avalanche,Physical,419,100,Avalanche,60,10,Ice",
"babydolleyes,Status,608,100,Baby-Doll Eyes,0,30,Fairy",
"barrage,Physical,140,85,Barrage,15,20,Normal",
"barrier,Status,112,true,Barrier,0,20,Psychic",
"batonpass,Status,226,true,Baton Pass,0,40,Normal",
"beatup,Physical,251,100,Beat Up,0,10,Dark",
"belch,Special,562,90,Belch,120,10,Poison",
"bellydrum,Status,187,true,Belly Drum,0,10,Normal",
"bestow,Status,516,true,Bestow,0,15,Normal",
"bide,Physical,117,true,Bide,0,10,Normal",
"bind,Physical,20,85,Bind,15,20,Normal",
"bite,Physical,44,100,Bite,60,25,Dark",
"blastburn,Special,307,90,Blast Burn,150,5,Fire",
"blazekick,Physical,299,90,Blaze Kick,85,10,Fire",
"blizzard,Special,59,70,Blizzard,110,5,Ice",
"block,Status,335,true,Block,0,5,Normal",
"blueflare,Special,551,85,Blue Flare,130,5,Fire",
"bodyslam,Physical,34,100,Body Slam,85,15,Normal",
"boltstrike,Physical,550,85,Bolt Strike,130,5,Electric",
"boneclub,Physical,125,85,Bone Club,65,20,Ground",
"bonerush,Physical,198,90,Bone Rush,25,10,Ground",
"bonemerang,Physical,155,90,Bonemerang,50,10,Ground",
"boomburst,Special,586,100,Boomburst,140,10,Normal",
"bounce,Physical,340,85,Bounce,85,5,Flying",
"bravebird,Physical,413,100,Brave Bird,120,15,Flying",
"brickbreak,Physical,280,100,Brick Break,75,15,Fighting",
"brine,Special,362,100,Brine,65,10,Water",
"bubble,Special,145,100,Bubble,40,30,Water",
"bubblebeam,Special,61,100,Bubble Beam,65,20,Water",
"bugbite,Physical,450,100,Bug Bite,60,20,Bug",
"bugbuzz,Special,405,100,Bug Buzz,90,10,Bug",
"bulkup,Status,339,true,Bulk Up,0,20,Fighting",
"bulldoze,Physical,523,100,Bulldoze,60,20,Ground",
"bulletpunch,Physical,418,100,Bullet Punch,40,30,Steel",
"bulletseed,Physical,331,100,Bullet Seed,25,30,Grass",
"calmmind,Status,347,true,Calm Mind,0,20,Psychic",
"camouflage,Status,293,true,Camouflage,0,20,Normal",
"captivate,Status,445,100,Captivate,0,20,Normal",
"celebrate,Status,606,true,Celebrate,0,40,Normal",
"charge,Status,268,true,Charge,0,20,Electric",
"chargebeam,Special,451,90,Charge Beam,50,10,Electric",
"charm,Status,204,100,Charm,0,20,Fairy",
"chatter,Special,448,100,Chatter,65,20,Flying",
"chipaway,Physical,498,100,Chip Away,70,20,Normal",
"circlethrow,Physical,509,90,Circle Throw,60,10,Fighting",
"clamp,Physical,128,85,Clamp,35,15,Water",
"clearsmog,Special,499,true,Clear Smog,50,15,Poison",
"closecombat,Physical,370,100,Close Combat,120,5,Fighting",
"coil,Status,489,true,Coil,0,20,Poison",
"cometpunch,Physical,4,85,Comet Punch,18,15,Normal",
"confide,Status,590,true,Confide,0,20,Normal",
"confuseray,Status,109,100,Confuse Ray,0,10,Ghost",
"confusion,Special,93,100,Confusion,50,25,Psychic",
"constrict,Physical,132,100,Constrict,10,35,Normal",
"conversion,Status,160,true,Conversion,0,30,Normal",
"conversion2,Status,176,true,Conversion 2,0,30,Normal",
"copycat,Status,383,true,Copycat,0,20,Normal",
"cosmicpower,Status,322,true,Cosmic Power,0,20,Psychic",
"cottonguard,Status,538,true,Cotton Guard,0,10,Grass",
"cottonspore,Status,178,100,Cotton Spore,0,40,Grass",
"counter,Physical,68,100,Counter,0,20,Fighting",
"covet,Physical,343,100,Covet,60,25,Normal",
"crabhammer,Physical,152,90,Crabhammer,100,10,Water",
"craftyshield,Status,578,true,Crafty Shield,0,10,Fairy",
"crosschop,Physical,238,80,Cross Chop,100,5,Fighting",
"crosspoison,Physical,440,100,Cross Poison,70,20,Poison",
"crunch,Physical,242,100,Crunch,80,15,Dark",
"crushclaw,Physical,306,95,Crush Claw,75,10,Normal",
"crushgrip,Physical,462,100,Crush Grip,0,5,Normal",
"curse,Status,174,true,Curse,0,10,Ghost",
"cut,Physical,15,95,Cut,50,30,Normal",
"darkpulse,Special,399,100,Dark Pulse,80,15,Dark",
"darkvoid,Status,464,80,Dark Void,0,10,Dark",
"dazzlinggleam,Special,605,100,Dazzling Gleam,80,10,Fairy",
"defendorder,Status,455,true,Defend Order,0,10,Bug",
"defensecurl,Status,111,true,Defense Curl,0,40,Normal",
"defog,Status,432,true,Defog,0,15,Flying",
"destinybond,Status,194,true,Destiny Bond,0,5,Ghost",
"detect,Status,197,true,Detect,0,5,Fighting",
"diamondstorm,Physical,591,95,Diamond Storm,100,5,Rock",
"dig,Physical,91,100,Dig,80,10,Ground",
"disable,Status,50,100,Disable,0,20,Normal",
"disarmingvoice,Special,574,true,Disarming Voice,40,15,Fairy",
"discharge,Special,435,100,Discharge,80,15,Electric",
"dive,Physical,291,100,Dive,80,10,Water",
"dizzypunch,Physical,146,100,Dizzy Punch,70,10,Normal",
"doomdesire,Special,353,100,Doom Desire,140,5,Steel",
"doubleedge,Physical,38,100,Double-Edge,120,15,Normal",
"doublehit,Physical,458,90,Double Hit,35,10,Normal",
"doublekick,Physical,24,100,Double Kick,30,30,Fighting",
"doubleslap,Physical,3,85,Double Slap,15,10,Normal",
"doubleteam,Status,104,true,Double Team,0,15,Normal",
"dracometeor,Special,434,90,Draco Meteor,130,5,Dragon",
"dragonascent,Physical,620,100,Dragon Ascent,120,5,Flying",
"dragonbreath,Special,225,100,Dragon Breath,60,20,Dragon",
"dragonclaw,Physical,337,100,Dragon Claw,80,15,Dragon",
"dragondance,Status,349,true,Dragon Dance,0,20,Dragon",
"dragonpulse,Special,406,100,Dragon Pulse,85,10,Dragon",
"dragonrage,Special,82,100,Dragon Rage,0,10,Dragon",
"dragonrush,Physical,407,75,Dragon Rush,100,10,Dragon",
"dragontail,Physical,525,90,Dragon Tail,60,10,Dragon",
"drainingkiss,Special,577,100,Draining Kiss,50,10,Fairy",
"drainpunch,Physical,409,100,Drain Punch,75,10,Fighting",
"dreameater,Special,138,100,Dream Eater,100,15,Psychic",
"drillpeck,Physical,65,100,Drill Peck,80,20,Flying",
"drillrun,Physical,529,95,Drill Run,80,10,Ground",
"dualchop,Physical,530,90,Dual Chop,40,15,Dragon",
"dynamicpunch,Physical,223,50,Dynamic Punch,100,5,Fighting",
"earthpower,Special,414,100,Earth Power,90,10,Ground",
"earthquake,Physical,89,100,Earthquake,100,10,Ground",
"echoedvoice,Special,497,100,Echoed Voice,40,15,Normal",
"eerieimpulse,Status,598,100,Eerie Impulse,0,15,Electric",
"eggbomb,Physical,121,75,Egg Bomb,100,10,Normal",
"electricterrain,Status,604,true,Electric Terrain,0,10,Electric",
"electrify,Status,582,true,Electrify,0,20,Electric",
"electroball,Special,486,100,Electro Ball,0,10,Electric",
"electroweb,Special,527,95,Electroweb,55,15,Electric",
"embargo,Status,373,100,Embargo,0,15,Dark",
"ember,Special,52,100,Ember,40,25,Fire",
"encore,Status,227,100,Encore,0,5,Normal",
"endeavor,Physical,283,100,Endeavor,0,5,Normal",
"endure,Status,203,true,Endure,0,10,Normal",
"energyball,Special,412,100,Energy Ball,90,10,Grass",
"entrainment,Status,494,100,Entrainment,0,15,Normal",
"eruption,Special,284,100,Eruption,150,5,Fire",
"explosion,Physical,153,100,Explosion,250,5,Normal",
"extrasensory,Special,326,100,Extrasensory,80,20,Psychic",
"extremespeed,Physical,245,100,Extreme Speed,80,5,Normal",
"facade,Physical,263,100,Facade,70,20,Normal",
"feintattack,Physical,185,true,Feint Attack,60,20,Dark",
"fairylock,Status,587,true,Fairy Lock,0,10,Fairy",
"fairywind,Special,584,100,Fairy Wind,40,30,Fairy",
"fakeout,Physical,252,100,Fake Out,40,10,Normal",
"faketears,Status,313,100,Fake Tears,0,20,Dark",
"falseswipe,Physical,206,100,False Swipe,40,40,Normal",
"featherdance,Status,297,100,Feather Dance,0,15,Flying",
"feint,Physical,364,100,Feint,30,10,Normal",
"fellstinger,Physical,565,100,Fell Stinger,30,25,Bug",
"fierydance,Special,552,100,Fiery Dance,80,10,Fire",
"finalgambit,Special,515,100,Final Gambit,0,5,Fighting",
"fireblast,Special,126,85,Fire Blast,110,5,Fire",
"firefang,Physical,424,95,Fire Fang,65,15,Fire",
"firepledge,Special,519,100,Fire Pledge,80,10,Fire",
"firepunch,Physical,7,100,Fire Punch,75,15,Fire",
"firespin,Special,83,85,Fire Spin,35,15,Fire",
"fissure,Physical,90,30,Fissure,0,5,Ground",
"flail,Physical,175,100,Flail,0,15,Normal",
"flameburst,Special,481,100,Flame Burst,70,15,Fire",
"flamecharge,Physical,488,100,Flame Charge,50,20,Fire",
"flamewheel,Physical,172,100,Flame Wheel,60,25,Fire",
"flamethrower,Special,53,100,Flamethrower,90,15,Fire",
"flareblitz,Physical,394,100,Flare Blitz,120,15,Fire",
"flash,Status,148,100,Flash,0,20,Normal",
"flashcannon,Special,430,100,Flash Cannon,80,10,Steel",
"flatter,Status,260,100,Flatter,0,15,Dark",
"fling,Physical,374,100,Fling,0,10,Dark",
"flowershield,Status,579,true,Flower Shield,0,10,Fairy",
"fly,Physical,19,95,Fly,90,15,Flying",
"flyingpress,Physical,560,95,Flying Press,80,10,Fighting",
"focusblast,Special,411,70,Focus Blast,120,5,Fighting",
"focusenergy,Status,116,true,Focus Energy,0,30,Normal",
"focuspunch,Physical,264,100,Focus Punch,150,20,Fighting",
"followme,Status,266,true,Follow Me,0,20,Normal",
"forcepalm,Physical,395,100,Force Palm,60,10,Fighting",
"foresight,Status,193,true,Foresight,0,40,Normal",
"forestscurse,Status,571,100,Forest's Curse,0,20,Grass",
"foulplay,Physical,492,100,Foul Play,95,15,Dark",
"freezedry,Special,573,100,Freeze-Dry,70,20,Ice",
"freezeshock,Physical,553,90,Freeze Shock,140,5,Ice",
"frenzyplant,Special,338,90,Frenzy Plant,150,5,Grass",
"frostbreath,Special,524,90,Frost Breath,60,10,Ice",
"frustration,Physical,218,100,Frustration,0,20,Normal",
"furyattack,Physical,31,85,Fury Attack,15,20,Normal",
"furycutter,Physical,210,95,Fury Cutter,40,20,Bug",
"furyswipes,Physical,154,80,Fury Swipes,18,15,Normal",
"fusionbolt,Physical,559,100,Fusion Bolt,100,5,Electric",
"fusionflare,Special,558,100,Fusion Flare,100,5,Fire",
"futuresight,Special,248,100,Future Sight,120,10,Psychic",
"gastroacid,Status,380,100,Gastro Acid,0,10,Poison",
"geargrind,Physical,544,85,Gear Grind,50,15,Steel",
"geomancy,Status,601,true,Geomancy,0,10,Fairy",
"gigadrain,Special,202,100,Giga Drain,75,10,Grass",
"gigaimpact,Physical,416,90,Giga Impact,150,5,Normal",
"glaciate,Special,549,95,Glaciate,65,10,Ice",
"glare,Status,137,100,Glare,0,30,Normal",
"grassknot,Special,447,100,Grass Knot,0,20,Grass",
"grasspledge,Special,520,100,Grass Pledge,80,10,Grass",
"grasswhistle,Status,320,55,Grass Whistle,0,15,Grass",
"grassyterrain,Status,580,true,Grassy Terrain,0,10,Grass",
"gravity,Status,356,true,Gravity,0,5,Psychic",
"growl,Status,45,100,Growl,0,40,Normal",
"growth,Status,74,true,Growth,0,20,Normal",
"grudge,Status,288,true,Grudge,0,5,Ghost",
"guardsplit,Status,470,true,Guard Split,0,10,Psychic",
"guardswap,Status,385,true,Guard Swap,0,10,Psychic",
"guillotine,Physical,12,30,Guillotine,0,5,Normal",
"gunkshot,Physical,441,80,Gunk Shot,120,5,Poison",
"gust,Special,16,100,Gust,40,35,Flying",
"gyroball,Physical,360,100,Gyro Ball,0,5,Steel",
"hail,Status,258,true,Hail,0,10,Ice",
"hammerarm,Physical,359,90,Hammer Arm,100,10,Fighting",
"happyhour,Status,603,true,Happy Hour,0,30,Normal",
"harden,Status,106,true,Harden,0,30,Normal",
"haze,Status,114,true,Haze,0,30,Ice",
"headcharge,Physical,543,100,Head Charge,120,15,Normal",
"headsmash,Physical,457,80,Head Smash,150,5,Rock",
"headbutt,Physical,29,100,Headbutt,70,15,Normal",
"healbell,Status,215,true,Heal Bell,0,5,Normal",
"healblock,Status,377,100,Heal Block,0,15,Psychic",
"healorder,Status,456,true,Heal Order,0,10,Bug",
"healpulse,Status,505,true,Heal Pulse,0,10,Psychic",
"healingwish,Status,361,true,Healing Wish,0,10,Psychic",
"heartstamp,Physical,531,100,Heart Stamp,60,25,Psychic",
"heartswap,Status,391,true,Heart Swap,0,10,Psychic",
"heatcrash,Physical,535,100,Heat Crash,0,10,Fire",
"heatwave,Special,257,90,Heat Wave,95,10,Fire",
"heavyslam,Physical,484,100,Heavy Slam,0,10,Steel",
"helpinghand,Status,270,true,Helping Hand,0,20,Normal",
"hex,Special,506,100,Hex,65,10,Ghost",
"hiddenpower,Special,237,100,Hidden Power,60,15,Normal",
"hiddenpowerbug,Special,undefined,100,Hidden Power Bug,60,15,Bug",
"hiddenpowerdark,Special,undefined,100,Hidden Power Dark,60,15,Dark",
"hiddenpowerdragon,Special,undefined,100,Hidden Power Dragon,60,15,Dragon",
"hiddenpowerelectric,Special,undefined,100,Hidden Power Electric,60,15,Electric",
"hiddenpowerfighting,Special,undefined,100,Hidden Power Fighting,60,15,Fighting",
"hiddenpowerfire,Special,undefined,100,Hidden Power Fire,60,15,Fire",
"hiddenpowerflying,Special,undefined,100,Hidden Power Flying,60,15,Flying",
"hiddenpowerghost,Special,undefined,100,Hidden Power Ghost,60,15,Ghost",
"hiddenpowergrass,Special,undefined,100,Hidden Power Grass,60,15,Grass",
"hiddenpowerground,Special,undefined,100,Hidden Power Ground,60,15,Ground",
"hiddenpowerice,Special,undefined,100,Hidden Power Ice,60,15,Ice",
"hiddenpowerpoison,Special,undefined,100,Hidden Power Poison,60,15,Poison",
"hiddenpowerpsychic,Special,undefined,100,Hidden Power Psychic,60,15,Psychic",
"hiddenpowerrock,Special,undefined,100,Hidden Power Rock,60,15,Rock",
"hiddenpowersteel,Special,undefined,100,Hidden Power Steel,60,15,Steel",
"hiddenpowerwater,Special,undefined,100,Hidden Power Water,60,15,Water",
"highjumpkick,Physical,136,90,High Jump Kick,130,10,Fighting",
"holdback,Physical,610,100,Hold Back,40,40,Normal",
"holdhands,Status,615,true,Hold Hands,0,40,Normal",
"honeclaws,Status,468,true,Hone Claws,0,15,Dark",
"hornattack,Physical,30,100,Horn Attack,65,25,Normal",
"horndrill,Physical,32,30,Horn Drill,0,5,Normal",
"hornleech,Physical,532,100,Horn Leech,75,10,Grass",
"howl,Status,336,true,Howl,0,40,Normal",
"hurricane,Special,542,70,Hurricane,110,10,Flying",
"hydrocannon,Special,308,90,Hydro Cannon,150,5,Water",
"hydropump,Special,56,80,Hydro Pump,110,5,Water",
"hyperbeam,Special,63,90,Hyper Beam,150,5,Normal",
"hyperfang,Physical,158,90,Hyper Fang,80,15,Normal",
"hyperspacefury,Physical,621,true,Hyperspace Fury,100,5,Dark",
"hyperspacehole,Special,593,true,Hyperspace Hole,80,5,Psychic",
"hypervoice,Special,304,100,Hyper Voice,90,10,Normal",
"hypnosis,Status,95,60,Hypnosis,0,20,Psychic",
"iceball,Physical,301,90,Ice Ball,30,20,Ice",
"icebeam,Special,58,100,Ice Beam,90,10,Ice",
"iceburn,Special,554,90,Ice Burn,140,5,Ice",
"icefang,Physical,423,95,Ice Fang,65,15,Ice",
"icepunch,Physical,8,100,Ice Punch,75,15,Ice",
"iceshard,Physical,420,100,Ice Shard,40,30,Ice",
"iciclecrash,Physical,556,90,Icicle Crash,85,10,Ice",
"iciclespear,Physical,333,100,Icicle Spear,25,30,Ice",
"icywind,Special,196,95,Icy Wind,55,15,Ice",
"imprison,Status,286,true,Imprison,0,10,Psychic",
"incinerate,Special,510,100,Incinerate,60,15,Fire",
"inferno,Special,517,50,Inferno,100,5,Fire",
"infestation,Special,611,100,Infestation,20,20,Bug",
"ingrain,Status,275,true,Ingrain,0,20,Grass",
"iondeluge,Status,569,true,Ion Deluge,0,25,Electric",
"irondefense,Status,334,true,Iron Defense,0,15,Steel",
"ironhead,Physical,442,100,Iron Head,80,15,Steel",
"irontail,Physical,231,75,Iron Tail,100,15,Steel",
"judgment,Special,449,100,Judgment,100,10,Normal",
"jumpkick,Physical,26,95,Jump Kick,100,10,Fighting",
"karatechop,Physical,2,100,Karate Chop,50,25,Fighting",
"kinesis,Status,134,80,Kinesis,0,15,Psychic",
"kingsshield,Status,588,true,King's Shield,0,10,Steel",
"knockoff,Physical,282,100,Knock Off,65,20,Dark",
"landswrath,Physical,616,100,Land's Wrath,90,10,Ground",
"lastresort,Physical,387,100,Last Resort,140,5,Normal",
"lavaplume,Special,436,100,Lava Plume,80,15,Fire",
"leafblade,Physical,348,100,Leaf Blade,90,15,Grass",
"leafstorm,Special,437,90,Leaf Storm,130,5,Grass",
"leaftornado,Special,536,90,Leaf Tornado,65,10,Grass",
"leechlife,Physical,141,100,Leech Life,20,15,Bug",
"leechseed,Status,73,90,Leech Seed,0,10,Grass",
"leer,Status,43,100,Leer,0,30,Normal",
"lick,Physical,122,100,Lick,30,30,Ghost",
"lightofruin,Special,617,90,Light of Ruin,140,5,Fairy",
"lightscreen,Status,113,true,Light Screen,0,30,Psychic",
"lockon,Status,199,true,Lock-On,0,5,Normal",
"lovelykiss,Status,142,75,Lovely Kiss,0,10,Normal",
"lowkick,Physical,67,100,Low Kick,0,20,Fighting",
"lowsweep,Physical,490,100,Low Sweep,65,20,Fighting",
"luckychant,Status,381,true,Lucky Chant,0,30,Normal",
"lunardance,Status,461,true,Lunar Dance,0,10,Psychic",
"lusterpurge,Special,295,100,Luster Purge,70,5,Psychic",
"machpunch,Physical,183,100,Mach Punch,40,30,Fighting",
"magiccoat,Status,277,true,Magic Coat,0,15,Psychic",
"magicroom,Status,478,true,Magic Room,0,10,Psychic",
"magicalleaf,Special,345,true,Magical Leaf,60,20,Grass",
"magmastorm,Special,463,75,Magma Storm,100,5,Fire",
"magnetbomb,Physical,443,true,Magnet Bomb,60,20,Steel",
"magneticflux,Status,602,true,Magnetic Flux,0,20,Electric",
"magnetrise,Status,393,true,Magnet Rise,0,10,Electric",
"magnitude,Physical,222,100,Magnitude,0,30,Ground",
"matblock,Status,561,true,Mat Block,0,10,Fighting",
"mefirst,Status,382,true,Me First,0,20,Normal",
"meanlook,Status,212,true,Mean Look,0,5,Normal",
"meditate,Status,96,true,Meditate,0,40,Psychic",
"megadrain,Special,72,100,Mega Drain,40,15,Grass",
"megakick,Physical,25,75,Mega Kick,120,5,Normal",
"megapunch,Physical,5,85,Mega Punch,80,20,Normal",
"megahorn,Physical,224,85,Megahorn,120,10,Bug",
"memento,Status,262,100,Memento,0,10,Dark",
"metalburst,Physical,368,100,Metal Burst,0,10,Steel",
"metalclaw,Physical,232,95,Metal Claw,50,35,Steel",
"metalsound,Status,319,85,Metal Sound,0,40,Steel",
"meteormash,Physical,309,90,Meteor Mash,90,10,Steel",
"metronome,Status,118,true,Metronome,0,10,Normal",
"milkdrink,Status,208,true,Milk Drink,0,10,Normal",
"mimic,Status,102,true,Mimic,0,10,Normal",
"mindreader,Status,170,true,Mind Reader,0,5,Normal",
"minimize,Status,107,true,Minimize,0,10,Normal",
"miracleeye,Status,357,true,Miracle Eye,0,40,Psychic",
"mirrorcoat,Special,243,100,Mirror Coat,0,20,Psychic",
"mirrormove,Status,119,true,Mirror Move,0,20,Flying",
"mirrorshot,Special,429,85,Mirror Shot,65,10,Steel",
"mist,Status,54,true,Mist,0,30,Ice",
"mistball,Special,296,100,Mist Ball,70,5,Psychic",
"mistyterrain,Status,581,true,Misty Terrain,0,10,Fairy",
"moonblast,Special,585,100,Moonblast,95,15,Fairy",
"moonlight,Status,236,true,Moonlight,0,5,Fairy",
"morningsun,Status,234,true,Morning Sun,0,5,Normal",
"mudslap,Special,189,100,Mud-Slap,20,10,Ground",
"mudbomb,Special,426,85,Mud Bomb,65,10,Ground",
"mudshot,Special,341,95,Mud Shot,55,15,Ground",
"mudsport,Status,300,true,Mud Sport,0,15,Ground",
"muddywater,Special,330,85,Muddy Water,90,10,Water",
"mysticalfire,Special,595,100,Mystical Fire,65,10,Fire",
"nastyplot,Status,417,true,Nasty Plot,0,20,Dark",
"naturalgift,Physical,363,100,Natural Gift,0,15,Normal",
"naturepower,Status,267,true,Nature Power,0,20,Normal",
"needlearm,Physical,302,100,Needle Arm,60,15,Grass",
"nightdaze,Special,539,95,Night Daze,85,10,Dark",
"nightshade,Special,101,100,Night Shade,0,15,Ghost",
"nightslash,Physical,400,100,Night Slash,70,15,Dark",
"nightmare,Status,171,100,Nightmare,0,15,Ghost",
"nobleroar,Status,568,100,Noble Roar,0,30,Normal",
"nuzzle,Physical,609,100,Nuzzle,20,20,Electric",
"oblivionwing,Special,613,100,Oblivion Wing,80,10,Flying",
"octazooka,Special,190,85,Octazooka,65,10,Water",
"odorsleuth,Status,316,true,Odor Sleuth,0,40,Normal",
"ominouswind,Special,466,100,Ominous Wind,60,5,Ghost",
"originpulse,Special,618,85,Origin Pulse,110,10,Water",
"outrage,Physical,200,100,Outrage,120,10,Dragon",
"overheat,Special,315,90,Overheat,130,5,Fire",
"painsplit,Status,220,true,Pain Split,0,20,Normal",
"paraboliccharge,Special,570,100,Parabolic Charge,50,20,Electric",
"partingshot,Status,575,100,Parting Shot,0,20,Dark",
"payday,Physical,6,100,Pay Day,40,20,Normal",
"payback,Physical,371,100,Payback,50,10,Dark",
"peck,Physical,64,100,Peck,35,35,Flying",
"perishsong,Status,195,true,Perish Song,0,5,Normal",
"petalblizzard,Physical,572,100,Petal Blizzard,90,15,Grass",
"petaldance,Special,80,100,Petal Dance,120,10,Grass",
"phantomforce,Physical,566,100,Phantom Force,90,10,Ghost",
"pinmissile,Physical,42,95,Pin Missile,25,20,Bug",
"playnice,Status,589,true,Play Nice,0,20,Normal",
"playrough,Physical,583,90,Play Rough,90,10,Fairy",
"pluck,Physical,365,100,Pluck,60,20,Flying",
"poisonfang,Physical,305,100,Poison Fang,50,15,Poison",
"poisongas,Status,139,90,Poison Gas,0,40,Poison",
"poisonjab,Physical,398,100,Poison Jab,80,20,Poison",
"poisonpowder,Status,77,75,Poison Powder,0,35,Poison",
"poisonsting,Physical,40,100,Poison Sting,15,35,Poison",
"poisontail,Physical,342,100,Poison Tail,50,25,Poison",
"pound,Physical,1,100,Pound,40,35,Normal",
"powder,Status,600,100,Powder,0,20,Bug",
"powdersnow,Special,181,100,Powder Snow,40,25,Ice",
"powergem,Special,408,100,Power Gem,80,20,Rock",
"powersplit,Status,471,true,Power Split,0,10,Psychic",
"powerswap,Status,384,true,Power Swap,0,10,Psychic",
"powertrick,Status,379,true,Power Trick,0,10,Psychic",
"poweruppunch,Physical,612,100,Power-Up Punch,40,20,Fighting",
"powerwhip,Physical,438,85,Power Whip,120,10,Grass",
"precipiceblades,Physical,619,85,Precipice Blades,120,10,Ground",
"present,Physical,217,90,Present,0,15,Normal",
"protect,Status,182,true,Protect,0,10,Normal",
"psybeam,Special,60,100,Psybeam,65,20,Psychic",
"psychup,Status,244,true,Psych Up,0,10,Normal",
"psychic,Special,94,100,Psychic,90,10,Psychic",
"psychoboost,Special,354,90,Psycho Boost,140,5,Psychic",
"psychocut,Physical,427,100,Psycho Cut,70,20,Psychic",
"psychoshift,Status,375,100,Psycho Shift,0,10,Psychic",
"psyshock,Special,473,100,Psyshock,80,10,Psychic",
"psystrike,Special,540,100,Psystrike,100,10,Psychic",
"psywave,Special,149,100,Psywave,0,15,Psychic",
"punishment,Physical,386,100,Punishment,0,5,Dark",
"pursuit,Physical,228,100,Pursuit,40,20,Dark",
"quash,Status,511,100,Quash,0,15,Dark",
"quickattack,Physical,98,100,Quick Attack,40,30,Normal",
"quickguard,Status,501,true,Quick Guard,0,15,Fighting",
"quiverdance,Status,483,true,Quiver Dance,0,20,Bug",
"rage,Physical,99,100,Rage,20,20,Normal",
"ragepowder,Status,476,true,Rage Powder,0,20,Bug",
"raindance,Status,240,true,Rain Dance,0,5,Water",
"rapidspin,Physical,229,100,Rapid Spin,20,40,Normal",
"razorleaf,Physical,75,95,Razor Leaf,55,25,Grass",
"razorshell,Physical,534,95,Razor Shell,75,10,Water",
"razorwind,Special,13,100,Razor Wind,80,10,Normal",
"recover,Status,105,true,Recover,0,10,Normal",
"recycle,Status,278,true,Recycle,0,10,Normal",
"reflect,Status,115,true,Reflect,0,20,Psychic",
"reflecttype,Status,513,true,Reflect Type,0,15,Normal",
"refresh,Status,287,true,Refresh,0,20,Normal",
"relicsong,Special,547,100,Relic Song,75,10,Normal",
"rest,Status,156,true,Rest,0,10,Psychic",
"retaliate,Physical,514,100,Retaliate,70,5,Normal",
"return,Physical,216,100,Return,0,20,Normal",
"revenge,Physical,279,100,Revenge,60,10,Fighting",
"reversal,Physical,179,100,Reversal,0,15,Fighting",
"roar,Status,46,true,Roar,0,20,Normal",
"roaroftime,Special,459,90,Roar of Time,150,5,Dragon",
"rockblast,Physical,350,90,Rock Blast,25,10,Rock",
"rockclimb,Physical,431,85,Rock Climb,90,20,Normal",
"rockpolish,Status,397,true,Rock Polish,0,20,Rock",
"rockslide,Physical,157,90,Rock Slide,75,10,Rock",
"rocksmash,Physical,249,100,Rock Smash,40,15,Fighting",
"rockthrow,Physical,88,90,Rock Throw,50,15,Rock",
"rocktomb,Physical,317,95,Rock Tomb,60,15,Rock",
"rockwrecker,Physical,439,90,Rock Wrecker,150,5,Rock",
"roleplay,Status,272,true,Role Play,0,10,Psychic",
"rollingkick,Physical,27,85,Rolling Kick,60,15,Fighting",
"rollout,Physical,205,90,Rollout,30,20,Rock",
"roost,Status,355,true,Roost,0,10,Flying",
"rototiller,Status,563,true,Rototiller,0,10,Ground",
"round,Special,496,100,Round,60,15,Normal",
"sacredfire,Physical,221,95,Sacred Fire,100,5,Fire",
"sacredsword,Physical,533,100,Sacred Sword,90,15,Fighting",
"safeguard,Status,219,true,Safeguard,0,25,Normal",
"sandattack,Status,28,100,Sand Attack,0,15,Ground",
"sandtomb,Physical,328,85,Sand Tomb,35,15,Ground",
"sandstorm,Status,201,true,Sandstorm,0,10,Rock",
"scald,Special,503,100,Scald,80,15,Water",
"scaryface,Status,184,100,Scary Face,0,10,Normal",
"scratch,Physical,10,100,Scratch,40,35,Normal",
"screech,Status,103,85,Screech,0,40,Normal",
"searingshot,Special,545,100,Searing Shot,100,5,Fire",
"secretpower,Physical,290,100,Secret Power,70,20,Normal",
"secretsword,Special,548,100,Secret Sword,85,10,Fighting",
"seedbomb,Physical,402,100,Seed Bomb,80,15,Grass",
"seedflare,Special,465,85,Seed Flare,120,5,Grass",
"seismictoss,Physical,69,100,Seismic Toss,0,20,Fighting",
"selfdestruct,Physical,120,100,Self-Destruct,200,5,Normal",
"shadowball,Special,247,100,Shadow Ball,80,15,Ghost",
"shadowclaw,Physical,421,100,Shadow Claw,70,15,Ghost",
"shadowforce,Physical,467,100,Shadow Force,120,5,Ghost",
"shadowpunch,Physical,325,true,Shadow Punch,60,20,Ghost",
"shadowsneak,Physical,425,100,Shadow Sneak,40,30,Ghost",
"sharpen,Status,159,true,Sharpen,0,30,Normal",
"sheercold,Special,329,30,Sheer Cold,0,5,Ice",
"shellsmash,Status,504,true,Shell Smash,0,15,Normal",
"shiftgear,Status,508,true,Shift Gear,0,10,Steel",
"shockwave,Special,351,true,Shock Wave,60,20,Electric",
"signalbeam,Special,324,100,Signal Beam,75,15,Bug",
"silverwind,Special,318,100,Silver Wind,60,5,Bug",
"simplebeam,Status,493,100,Simple Beam,0,15,Normal",
"sing,Status,47,55,Sing,0,15,Normal",
"sketch,Status,166,true,Sketch,0,1,Normal",
"skillswap,Status,285,true,Skill Swap,0,10,Psychic",
"skullbash,Physical,130,100,Skull Bash,130,10,Normal",
"skyattack,Physical,143,90,Sky Attack,140,5,Flying",
"skydrop,Physical,507,100,Sky Drop,60,10,Flying",
"skyuppercut,Physical,327,90,Sky Uppercut,85,15,Fighting",
"slackoff,Status,303,true,Slack Off,0,10,Normal",
"slam,Physical,21,75,Slam,80,20,Normal",
"slash,Physical,163,100,Slash,70,20,Normal",
"sleeppowder,Status,79,75,Sleep Powder,0,15,Grass",
"sleeptalk,Status,214,true,Sleep Talk,0,10,Normal",
"sludge,Special,124,100,Sludge,65,20,Poison",
"sludgebomb,Special,188,100,Sludge Bomb,90,10,Poison",
"sludgewave,Special,482,100,Sludge Wave,95,10,Poison",
"smackdown,Physical,479,100,Smack Down,50,15,Rock",
"smellingsalts,Physical,265,100,Smelling Salts,70,10,Normal",
"smog,Special,123,70,Smog,30,20,Poison",
"smokescreen,Status,108,100,Smokescreen,0,20,Normal",
"snarl,Special,555,95,Snarl,55,15,Dark",
"snatch,Status,289,true,Snatch,0,10,Dark",
"snore,Special,173,100,Snore,50,15,Normal",
"spikyshield,Status,596,true,Spiky Shield,0,10,Grass",
"soak,Status,487,100,Soak,0,20,Water",
"softboiled,Status,135,true,Soft-Boiled,0,10,Normal",
"solarbeam,Special,76,100,Solar Beam,120,10,Grass",
"sonicboom,Special,49,90,Sonic Boom,0,20,Normal",
"spacialrend,Special,460,95,Spacial Rend,100,5,Dragon",
"spark,Physical,209,100,Spark,65,20,Electric",
"spiderweb,Status,169,true,Spider Web,0,10,Bug",
"spikecannon,Physical,131,100,Spike Cannon,20,15,Normal",
"spikes,Status,191,true,Spikes,0,20,Ground",
"spitup,Special,255,100,Spit Up,0,10,Normal",
"spite,Status,180,100,Spite,0,10,Ghost",
"splash,Status,150,true,Splash,0,40,Normal",
"spore,Status,147,100,Spore,0,15,Grass",
"stealthrock,Status,446,true,Stealth Rock,0,20,Rock",
"steameruption,Special,592,95,Steam Eruption,110,5,Water",
"steelwing,Physical,211,90,Steel Wing,70,25,Steel",
"stickyweb,Status,564,true,Sticky Web,0,20,Bug",
"stockpile,Status,254,true,Stockpile,0,20,Normal",
"stomp,Physical,23,100,Stomp,65,20,Normal",
"stoneedge,Physical,444,80,Stone Edge,100,5,Rock",
"storedpower,Special,500,100,Stored Power,20,10,Psychic",
"stormthrow,Physical,480,100,Storm Throw,60,10,Fighting",
"steamroller,Physical,537,100,Steamroller,65,20,Bug",
"strength,Physical,70,100,Strength,80,15,Normal",
"stringshot,Status,81,95,String Shot,0,40,Bug",
"struggle,Physical,165,true,Struggle,50,1,Normal",
"strugglebug,Special,522,100,Struggle Bug,50,20,Bug",
"stunspore,Status,78,75,Stun Spore,0,30,Grass",
"submission,Physical,66,80,Submission,80,20,Fighting",
"substitute,Status,164,true,Substitute,0,10,Normal",
"suckerpunch,Physical,389,100,Sucker Punch,80,5,Dark",
"sunnyday,Status,241,true,Sunny Day,0,5,Fire",
"superfang,Physical,162,90,Super Fang,0,10,Normal",
"superpower,Physical,276,100,Superpower,120,5,Fighting",
"supersonic,Status,48,55,Supersonic,0,20,Normal",
"surf,Special,57,100,Surf,90,15,Water",
"swagger,Status,207,90,Swagger,0,15,Normal",
"swallow,Status,256,true,Swallow,0,10,Normal",
"sweetkiss,Status,186,75,Sweet Kiss,0,10,Fairy",
"sweetscent,Status,230,100,Sweet Scent,0,20,Normal",
"swift,Special,129,true,Swift,60,20,Normal",
"switcheroo,Status,415,100,Switcheroo,0,10,Dark",
"swordsdance,Status,14,true,Swords Dance,0,20,Normal",
"synchronoise,Special,485,100,Synchronoise,120,10,Psychic",
"synthesis,Status,235,true,Synthesis,0,5,Grass",
"tackle,Physical,33,100,Tackle,50,35,Normal",
"tailglow,Status,294,true,Tail Glow,0,20,Bug",
"tailslap,Physical,541,85,Tail Slap,25,10,Normal",
"tailwhip,Status,39,100,Tail Whip,0,30,Normal",
"tailwind,Status,366,true,Tailwind,0,15,Flying",
"takedown,Physical,36,85,Take Down,90,20,Normal",
"taunt,Status,269,100,Taunt,0,20,Dark",
"technoblast,Special,546,100,Techno Blast,120,5,Normal",
"teeterdance,Status,298,100,Teeter Dance,0,20,Normal",
"telekinesis,Status,477,true,Telekinesis,0,15,Psychic",
"teleport,Status,100,true,Teleport,0,20,Psychic",
"thief,Physical,168,100,Thief,60,25,Dark",
"thousandarrows,Physical,614,100,Thousand Arrows,90,10,Ground",
"thousandwaves,Physical,615,100,Thousand Waves,90,10,Ground",
"thrash,Physical,37,100,Thrash,120,10,Normal",
"thunder,Special,87,70,Thunder,110,10,Electric",
"thunderfang,Physical,422,95,Thunder Fang,65,15,Electric",
"thunderpunch,Physical,9,100,Thunder Punch,75,15,Electric",
"thundershock,Special,84,100,Thunder Shock,40,30,Electric",
"thunderwave,Status,86,100,Thunder Wave,0,20,Electric",
"thunderbolt,Special,85,100,Thunderbolt,90,15,Electric",
"tickle,Status,321,100,Tickle,0,20,Normal",
"topsyturvy,Status,576,true,Topsy-Turvy,0,20,Dark",
"torment,Status,259,100,Torment,0,15,Dark",
"toxic,Status,92,90,Toxic,0,10,Poison",
"toxicspikes,Status,390,true,Toxic Spikes,0,20,Poison",
"transform,Status,144,true,Transform,0,10,Normal",
"triattack,Special,161,100,Tri Attack,80,10,Normal",
"trick,Status,271,100,Trick,0,10,Psychic",
"trickortreat,Status,567,100,Trick-or-Treat,0,20,Ghost",
"trickroom,Status,433,true,Trick Room,0,5,Psychic",
"triplekick,Physical,167,90,Triple Kick,10,10,Fighting",
"trumpcard,Special,376,true,Trump Card,0,5,Normal",
"twineedle,Physical,41,100,Twineedle,25,20,Bug",
"twister,Special,239,100,Twister,40,20,Dragon",
"uturn,Physical,369,100,U-turn,70,20,Bug",
"uproar,Special,253,100,Uproar,90,10,Normal",
"vcreate,Physical,557,95,V-create,180,5,Fire",
"vacuumwave,Special,410,100,Vacuum Wave,40,30,Fighting",
"venomdrench,Status,599,100,Venom Drench,0,20,Poison",
"venoshock,Special,474,100,Venoshock,65,10,Poison",
"vicegrip,Physical,11,100,Vice Grip,55,30,Normal",
"vinewhip,Physical,22,100,Vine Whip,45,25,Grass",
"vitalthrow,Physical,233,true,Vital Throw,70,10,Fighting",
"voltswitch,Special,521,100,Volt Switch,70,20,Electric",
"volttackle,Physical,344,100,Volt Tackle,120,15,Electric",
"wakeupslap,Physical,358,100,Wake-Up Slap,70,10,Fighting",
"watergun,Special,55,100,Water Gun,40,25,Water",
"waterpledge,Special,518,100,Water Pledge,80,10,Water",
"waterpulse,Special,352,100,Water Pulse,60,20,Water",
"watersport,Status,346,true,Water Sport,0,15,Water",
"waterspout,Special,323,100,Water Spout,150,5,Water",
"waterfall,Physical,127,100,Waterfall,80,15,Water",
"watershuriken,Physical,594,100,Water Shuriken,15,20,Water",
"weatherball,Special,311,100,Weather Ball,50,10,Normal",
"whirlpool,Special,250,85,Whirlpool,35,15,Water",
"whirlwind,Status,18,true,Whirlwind,0,20,Normal",
"wideguard,Status,469,true,Wide Guard,0,10,Rock",
"wildcharge,Physical,528,100,Wild Charge,90,15,Electric",
"willowisp,Status,261,85,Will-O-Wisp,0,15,Fire",
"wingattack,Physical,17,100,Wing Attack,60,35,Flying",
"wish,Status,273,true,Wish,0,10,Normal",
"withdraw,Status,110,true,Withdraw,0,40,Water",
"wonderroom,Status,472,true,Wonder Room,0,10,Psychic",
"woodhammer,Physical,452,100,Wood Hammer,120,15,Grass",
"workup,Status,526,true,Work Up,0,30,Normal",
"worryseed,Status,388,100,Worry Seed,0,10,Grass",
"wrap,Physical,35,90,Wrap,15,20,Normal",
"wringout,Special,378,100,Wring Out,0,5,Normal",
"xscissor,Physical,404,100,X-Scissor,80,15,Bug",
"yawn,Status,281,true,Yawn,0,10,Normal",
"zapcannon,Special,192,50,Zap Cannon,120,5,Electric",
"zenheadbutt,Physical,428,90,Zen Headbutt,80,15,Psychic",
"paleowave,Special,undefined,100,Paleo Wave,85,15,Rock",
"shadowstrike,Physical,undefined,95,Shadow Strike,80,10,Ghost",
"magikarpsrevenge,Physical,undefined,true,Magikarp's Revenge,120,10,Water"};
        string[][] types = new string[][]{
new string[]{"Normal"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Fire","Flying"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Bug"},
new string[]{"Bug"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Poison"},
new string[]{"Bug","Poison"},
new string[]{"Bug","Poison"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Ground"},
new string[]{"Ground"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Poison","Ground"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Poison","Ground"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Poison","Flying"},
new string[]{"Poison","Flying"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Bug","Grass"},
new string[]{"Bug","Grass"},
new string[]{"Bug","Poison"},
new string[]{"Bug","Poison"},
new string[]{"Ground"},
new string[]{"Ground"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water","Fighting"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Water","Poison"},
new string[]{"Water","Poison"},
new string[]{"Rock","Ground"},
new string[]{"Rock","Ground"},
new string[]{"Rock","Ground"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Water","Psychic"},
new string[]{"Water","Psychic"},
new string[]{"Electric","Steel"},
new string[]{"Electric","Steel"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Water"},
new string[]{"Water","Ice"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Water"},
new string[]{"Water","Ice"},
new string[]{"Ghost","Poison"},
new string[]{"Ghost","Poison"},
new string[]{"Ghost","Poison"},
new string[]{"Rock","Ground"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Grass","Psychic"},
new string[]{"Grass","Psychic"},
new string[]{"Ground"},
new string[]{"Ground"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Normal"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Ground","Rock"},
new string[]{"Ground","Rock"},
new string[]{"Normal"},
new string[]{"Grass"},
new string[]{"Normal"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water","Psychic"},
new string[]{"Psychic"},
new string[]{"Bug","Flying"},
new string[]{"Ice","Psychic"},
new string[]{"Electric"},
new string[]{"Fire"},
new string[]{"Bug"},
new string[]{"Normal"},
new string[]{"Water"},
new string[]{"Water","Flying"},
new string[]{"Water","Ice"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Water"},
new string[]{"Electric"},
new string[]{"Fire"},
new string[]{"Normal"},
new string[]{"Rock","Water"},
new string[]{"Rock","Water"},
new string[]{"Rock","Water"},
new string[]{"Rock","Water"},
new string[]{"Rock","Flying"},
new string[]{"Normal"},
new string[]{"Ice","Flying"},
new string[]{"Electric","Flying"},
new string[]{"Fire","Flying"},
new string[]{"Dragon"},
new string[]{"Dragon"},
new string[]{"Dragon","Flying"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Fire"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Poison"},
new string[]{"Bug","Poison"},
new string[]{"Poison","Flying"},
new string[]{"Water","Electric"},
new string[]{"Water","Electric"},
new string[]{"Electric"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal","Flying"},
new string[]{"Psychic","Flying"},
new string[]{"Psychic","Flying"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Grass"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Rock"},
new string[]{"Water"},
new string[]{"Grass","Flying"},
new string[]{"Grass","Flying"},
new string[]{"Grass","Flying"},
new string[]{"Normal"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Bug","Flying"},
new string[]{"Water","Ground"},
new string[]{"Water","Ground"},
new string[]{"Psychic"},
new string[]{"Dark"},
new string[]{"Dark","Flying"},
new string[]{"Water","Psychic"},
new string[]{"Ghost"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Normal","Psychic"},
new string[]{"Bug"},
new string[]{"Bug","Steel"},
new string[]{"Normal"},
new string[]{"Ground","Flying"},
new string[]{"Steel","Ground"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Water","Poison"},
new string[]{"Bug","Steel"},
new string[]{"Bug","Rock"},
new string[]{"Bug","Fighting"},
new string[]{"Dark","Ice"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Fire"},
new string[]{"Fire","Rock"},
new string[]{"Ice","Ground"},
new string[]{"Ice","Ground"},
new string[]{"Water","Rock"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Ice","Flying"},
new string[]{"Water","Flying"},
new string[]{"Steel","Flying"},
new string[]{"Dark","Fire"},
new string[]{"Dark","Fire"},
new string[]{"Water","Dragon"},
new string[]{"Ground"},
new string[]{"Ground"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Ice","Psychic"},
new string[]{"Electric"},
new string[]{"Fire"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Electric"},
new string[]{"Fire"},
new string[]{"Water"},
new string[]{"Rock","Ground"},
new string[]{"Rock","Ground"},
new string[]{"Rock","Dark"},
new string[]{"Psychic","Flying"},
new string[]{"Fire","Flying"},
new string[]{"Psychic","Grass"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Fire"},
new string[]{"Fire","Fighting"},
new string[]{"Fire","Fighting"},
new string[]{"Water"},
new string[]{"Water","Ground"},
new string[]{"Water","Ground"},
new string[]{"Dark"},
new string[]{"Dark"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Bug"},
new string[]{"Bug"},
new string[]{"Bug","Flying"},
new string[]{"Bug"},
new string[]{"Bug","Poison"},
new string[]{"Water","Grass"},
new string[]{"Water","Grass"},
new string[]{"Water","Grass"},
new string[]{"Grass"},
new string[]{"Grass","Dark"},
new string[]{"Grass","Dark"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Water","Flying"},
new string[]{"Water","Flying"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Bug","Water"},
new string[]{"Bug","Flying"},
new string[]{"Grass"},
new string[]{"Grass","Fighting"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Bug","Ground"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Ghost"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Fighting"},
new string[]{"Fighting"},
new string[]{"Normal"},
new string[]{"Rock"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Dark","Ghost"},
new string[]{"Steel"},
new string[]{"Steel","Rock"},
new string[]{"Steel","Rock"},
new string[]{"Steel","Rock"},
new string[]{"Fighting","Psychic"},
new string[]{"Fighting","Psychic"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Bug"},
new string[]{"Bug"},
new string[]{"Grass","Poison"},
new string[]{"Poison"},
new string[]{"Poison"},
new string[]{"Water","Dark"},
new string[]{"Water","Dark"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Fire","Ground"},
new string[]{"Fire","Ground"},
new string[]{"Fire"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Normal"},
new string[]{"Ground"},
new string[]{"Ground","Dragon"},
new string[]{"Ground","Dragon"},
new string[]{"Grass"},
new string[]{"Grass","Dark"},
new string[]{"Normal","Flying"},
new string[]{"Dragon","Flying"},
new string[]{"Normal"},
new string[]{"Poison"},
new string[]{"Rock","Psychic"},
new string[]{"Rock","Psychic"},
new string[]{"Water","Ground"},
new string[]{"Water","Ground"},
new string[]{"Water"},
new string[]{"Water","Dark"},
new string[]{"Ground","Psychic"},
new string[]{"Ground","Psychic"},
new string[]{"Rock","Grass"},
new string[]{"Rock","Grass"},
new string[]{"Rock","Bug"},
new string[]{"Rock","Bug"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Ghost"},
new string[]{"Ghost"},
new string[]{"Ghost"},
new string[]{"Ghost"},
new string[]{"Grass","Flying"},
new string[]{"Psychic"},
new string[]{"Dark"},
new string[]{"Psychic"},
new string[]{"Ice"},
new string[]{"Ice"},
new string[]{"Ice","Water"},
new string[]{"Ice","Water"},
new string[]{"Ice","Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water","Rock"},
new string[]{"Water"},
new string[]{"Dragon"},
new string[]{"Dragon"},
new string[]{"Dragon","Flying"},
new string[]{"Steel","Psychic"},
new string[]{"Steel","Psychic"},
new string[]{"Steel","Psychic"},
new string[]{"Rock"},
new string[]{"Ice"},
new string[]{"Steel"},
new string[]{"Dragon","Psychic"},
new string[]{"Dragon","Psychic"},
new string[]{"Water"},
new string[]{"Ground"},
new string[]{"Dragon","Flying"},
new string[]{"Steel","Psychic"},
new string[]{"Psychic"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Grass","Ground"},
new string[]{"Fire"},
new string[]{"Fire","Fighting"},
new string[]{"Fire","Fighting"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water","Steel"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Normal","Flying"},
new string[]{"Normal"},
new string[]{"Normal","Water"},
new string[]{"Bug"},
new string[]{"Bug"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Electric"},
new string[]{"Grass","Poison"},
new string[]{"Grass","Poison"},
new string[]{"Rock"},
new string[]{"Rock"},
new string[]{"Rock","Steel"},
new string[]{"Rock","Steel"},
new string[]{"Bug"},
new string[]{"Bug","Grass"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Flying"},
new string[]{"Bug","Flying"},
new string[]{"Electric"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Grass"},
new string[]{"Grass"},
new string[]{"Water"},
new string[]{"Water","Ground"},
new string[]{"Normal"},
new string[]{"Ghost","Flying"},
new string[]{"Ghost","Flying"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Ghost"},
new string[]{"Dark","Flying"},
new string[]{"Normal"},
new string[]{"Normal"},
new string[]{"Psychic"},
new string[]{"Poison","Dark"},
new string[]{"Poison","Dark"},
new string[]{"Steel","Psychic"},
new string[]{"Steel","Psychic"},
new string[]{"Rock"},
new string[]{"Psychic"},
new string[]{"Normal"},
new string[]{"Normal","Flying"},
new string[]{"Ghost","Dark"},
new string[]{"Dragon","Ground"},
new string[]{"Dragon","Ground"},
new string[]{"Dragon","Ground"},
new string[]{"Normal"},
new string[]{"Fighting"},
new string[]{"Fighting","Steel"},
new string[]{"Ground"},
new string[]{"Ground"},
new string[]{"Poison","Bug"},
new string[]{"Poison","Dark"},
new string[]{"Poison","Fighting"},
new string[]{"Poison","Fighting"},
new string[]{"Grass"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Water","Flying"},
new string[]{"Grass","Ice"},
new string[]{"Grass","Ice"},
new string[]{"Dark","Ice"},
new string[]{"Electric","Steel"},
new string[]{"Normal"},
new string[]{"Ground","Rock"},
new string[]{"Grass"},
new string[]{"Electric"},
new string[]{"Fire"},
new string[]{"Normal","Flying"},
new string[]{"Bug","Flying"},
new string[]{"Grass"},
new string[]{"Ice"},
new string[]{"Ground","Flying"},
new string[]{"Ice","Ground"},
new string[]{"Normal"},
new string[]{"Psychic","Fighting"},
new string[]{"Rock","Steel"},
new string[]{"Ghost"},
new string[]{"Ice","Ghost"},
new string[]{"Electric","Ghost"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Psychic"},
new string[]{"Steel","Dragon"},
new string[]{"Water","Dragon"},
new string[]{"Fire","Steel"},
new string[]{"Normal"},
new string[]{"Ghost","Dragon"},
new string[]{"Psychic"},
new string[]{"Water"},
new string[]{"Water"},
new string[]{"Dark"},
new string[]{"Grass"},
new string[]{"Normal"}
        };
        int[][] basestats = new int[][]{
            new int[]{0,0,0,0,0,0},
        new int[]{45,49,49,65,65,45},
new int[]{60,62,63,80,80,60},
new int[]{80,82,83,100,100,80},
new int[]{39,52,43,60,50,65},
new int[]{58,64,58,80,65,80},
new int[]{78,84,78,109,85,100},
new int[]{44,48,65,50,64,43},
new int[]{59,63,80,65,80,58},
new int[]{79,83,100,85,105,78},
new int[]{45,30,35,20,20,45},
new int[]{50,20,55,25,25,30},
new int[]{60,45,50,80,80,70},
new int[]{40,35,30,20,20,50},
new int[]{45,25,50,25,25,35},
new int[]{65,80,40,45,80,75},
new int[]{40,45,40,35,35,56},
new int[]{63,60,55,50,50,71},
new int[]{83,80,75,70,70,91},
new int[]{30,56,35,25,35,72},
new int[]{55,81,60,50,70,97},
new int[]{40,60,30,31,31,70},
new int[]{65,90,65,61,61,100},
new int[]{35,60,44,40,54,55},
new int[]{60,85,69,65,79,80},
new int[]{35,55,30,50,40,90},
new int[]{60,90,55,90,80,100},
new int[]{50,75,85,20,30,40},
new int[]{75,100,110,45,55,65},
new int[]{55,47,52,40,40,41},
new int[]{70,62,67,55,55,56},
new int[]{90,82,87,75,85,76},
new int[]{46,57,40,40,40,50},
new int[]{61,72,57,55,55,65},
new int[]{81,92,77,85,75,85},
new int[]{70,45,48,60,65,35},
new int[]{95,70,73,85,90,60},
new int[]{38,41,40,50,65,65},
new int[]{73,76,75,81,100,100},
new int[]{115,45,20,45,25,20},
new int[]{140,70,45,75,50,45},
new int[]{40,45,35,30,40,55},
new int[]{75,80,70,65,75,90},
new int[]{45,50,55,75,65,30},
new int[]{60,65,70,85,75,40},
new int[]{75,80,85,100,90,50},
new int[]{35,70,55,45,55,25},
new int[]{60,95,80,60,80,30},
new int[]{60,55,50,40,55,45},
new int[]{70,65,60,90,75,90},
new int[]{10,55,25,35,45,95},
new int[]{35,80,50,50,70,120},
new int[]{40,45,35,40,40,90},
new int[]{65,70,60,65,65,115},
new int[]{50,52,48,65,50,55},
new int[]{80,82,78,95,80,85},
new int[]{40,80,35,35,45,70},
new int[]{65,105,60,60,70,95},
new int[]{55,70,45,70,50,60},
new int[]{90,110,80,100,80,95},
new int[]{40,50,40,40,40,90},
new int[]{65,65,65,50,50,90},
new int[]{90,85,95,70,90,70},
new int[]{25,20,15,105,55,90},
new int[]{40,35,30,120,70,105},
new int[]{55,50,45,135,85,120},
new int[]{70,80,50,35,35,35},
new int[]{80,100,70,50,60,45},
new int[]{90,130,80,65,85,55},
new int[]{50,75,35,70,30,40},
new int[]{65,90,50,85,45,55},
new int[]{80,105,65,100,60,70},
new int[]{40,40,35,50,100,70},
new int[]{80,70,65,80,120,100},
new int[]{40,80,100,30,30,20},
new int[]{55,95,115,45,45,35},
new int[]{80,110,130,55,65,45},
new int[]{50,85,55,65,65,90},
new int[]{65,100,70,80,80,105},
new int[]{90,65,65,40,40,15},
new int[]{95,75,110,100,80,30},
new int[]{25,35,70,95,55,45},
new int[]{50,60,95,120,70,70},
new int[]{52,65,55,58,62,60},
new int[]{35,85,45,35,35,75},
new int[]{60,110,70,60,60,100},
new int[]{65,45,55,45,70,45},
new int[]{90,70,80,70,95,70},
new int[]{80,80,50,40,50,25},
new int[]{105,105,75,65,100,50},
new int[]{30,65,100,45,25,40},
new int[]{50,95,180,85,45,70},
new int[]{30,35,30,100,35,80},
new int[]{45,50,45,115,55,95},
new int[]{60,65,60,130,75,110},
new int[]{35,45,160,30,45,70},
new int[]{60,48,45,43,90,42},
new int[]{85,73,70,73,115,67},
new int[]{30,105,90,25,25,50},
new int[]{55,130,115,50,50,75},
new int[]{40,30,50,55,55,100},
new int[]{60,50,70,80,80,140},
new int[]{60,40,80,60,45,40},
new int[]{95,95,85,125,65,55},
new int[]{50,50,95,40,50,35},
new int[]{60,80,110,50,80,45},
new int[]{50,120,53,35,110,87},
new int[]{50,105,79,35,110,76},
new int[]{90,55,75,60,75,30},
new int[]{40,65,95,60,45,35},
new int[]{65,90,120,85,70,60},
new int[]{80,85,95,30,30,25},
new int[]{105,130,120,45,45,40},
new int[]{250,5,5,35,105,50},
new int[]{65,55,115,100,40,60},
new int[]{105,95,80,40,80,90},
new int[]{30,40,70,70,25,60},
new int[]{55,65,95,95,45,85},
new int[]{45,67,60,35,50,63},
new int[]{80,92,65,65,80,68},
new int[]{30,45,55,70,55,85},
new int[]{60,75,85,100,85,115},
new int[]{40,45,65,100,120,90},
new int[]{70,110,80,55,80,105},
new int[]{65,50,35,115,95,95},
new int[]{65,83,57,95,85,105},
new int[]{65,95,57,100,85,93},
new int[]{65,125,100,55,70,85},
new int[]{75,100,95,40,70,110},
new int[]{20,10,55,15,20,80},
new int[]{95,125,79,60,100,81},
new int[]{130,85,80,85,95,60},
new int[]{48,48,48,48,48,48},
new int[]{55,55,50,45,65,55},
new int[]{130,65,60,110,95,65},
new int[]{65,65,60,110,95,130},
new int[]{65,130,60,95,110,65},
new int[]{65,60,70,85,75,40},
new int[]{35,40,100,90,55,35},
new int[]{70,60,125,115,70,55},
new int[]{30,80,90,55,45,55},
new int[]{60,115,105,65,70,80},
new int[]{80,105,65,60,75,130},
new int[]{160,110,65,65,110,30},
new int[]{90,85,100,95,125,85},
new int[]{90,90,85,125,90,100},
new int[]{90,100,90,125,85,90},
new int[]{41,64,45,50,50,50},
new int[]{61,84,65,70,70,70},
new int[]{91,134,95,100,100,80},
new int[]{106,110,90,154,90,130},
new int[]{100,100,100,100,100,100},
new int[]{45,49,65,49,65,45},
new int[]{60,62,80,63,80,60},
new int[]{80,82,100,83,100,80},
new int[]{39,52,43,60,50,65},
new int[]{58,64,58,80,65,80},
new int[]{78,84,78,109,85,100},
new int[]{50,65,64,44,48,43},
new int[]{65,80,80,59,63,58},
new int[]{85,105,100,79,83,78},
new int[]{35,46,34,35,45,20},
new int[]{85,76,64,45,55,90},
new int[]{60,30,30,36,56,50},
new int[]{100,50,50,76,96,70},
new int[]{40,20,30,40,80,55},
new int[]{55,35,50,55,110,85},
new int[]{40,60,40,40,40,30},
new int[]{70,90,70,60,60,40},
new int[]{85,90,80,70,80,130},
new int[]{75,38,38,56,56,67},
new int[]{125,58,58,76,76,67},
new int[]{20,40,15,35,35,60},
new int[]{50,25,28,45,55,15},
new int[]{90,30,15,40,20,15},
new int[]{35,20,65,40,65,20},
new int[]{55,40,85,80,105,40},
new int[]{40,50,45,70,45,70},
new int[]{65,75,70,95,70,95},
new int[]{55,40,40,65,45,35},
new int[]{70,55,55,80,60,45},
new int[]{90,75,75,115,90,55},
new int[]{75,80,85,90,100,50},
new int[]{70,20,50,20,50,40},
new int[]{100,50,80,50,80,50},
new int[]{70,100,115,30,65,30},
new int[]{90,75,75,90,100,70},
new int[]{35,35,40,35,55,50},
new int[]{55,45,50,45,65,80},
new int[]{75,55,70,55,85,110},
new int[]{55,70,55,40,55,85},
new int[]{30,30,30,30,30,30},
new int[]{75,75,55,105,85,30},
new int[]{65,65,45,75,45,95},
new int[]{55,45,45,25,25,15},
new int[]{95,85,85,65,65,35},
new int[]{65,65,60,130,95,110},
new int[]{95,65,110,60,130,65},
new int[]{60,85,42,85,42,91},
new int[]{95,75,80,100,110,30},
new int[]{60,60,60,85,85,85},
new int[]{48,72,48,72,48,48},
new int[]{190,33,58,33,58,33},
new int[]{70,80,65,90,65,85},
new int[]{50,65,90,35,35,15},
new int[]{75,90,140,60,60,40},
new int[]{100,70,70,65,65,45},
new int[]{65,75,105,35,65,85},
new int[]{75,85,200,55,65,30},
new int[]{60,80,50,40,40,30},
new int[]{90,120,75,60,60,45},
new int[]{65,95,75,55,55,85},
new int[]{70,130,100,55,80,65},
new int[]{20,10,230,10,230,5},
new int[]{80,125,75,40,95,85},
new int[]{55,95,55,35,75,115},
new int[]{60,80,50,50,50,40},
new int[]{90,130,75,75,75,55},
new int[]{40,40,40,70,40,20},
new int[]{50,50,120,80,80,30},
new int[]{50,50,40,30,30,50},
new int[]{100,100,80,60,60,50},
new int[]{55,55,85,65,85,35},
new int[]{35,65,35,65,35,65},
new int[]{75,105,75,105,75,45},
new int[]{45,55,45,65,45,75},
new int[]{65,40,70,80,140,70},
new int[]{65,80,140,40,70,70},
new int[]{45,60,30,80,50,65},
new int[]{75,90,50,110,80,95},
new int[]{75,95,95,95,95,85},
new int[]{90,60,60,40,40,40},
new int[]{90,120,120,60,60,50},
new int[]{85,80,90,105,95,60},
new int[]{73,95,62,85,65,85},
new int[]{55,20,35,20,45,75},
new int[]{35,35,35,35,35,35},
new int[]{50,95,95,35,110,70},
new int[]{45,30,15,85,65,65},
new int[]{45,63,37,65,55,95},
new int[]{45,75,37,70,55,83},
new int[]{95,80,105,40,70,100},
new int[]{255,10,10,75,135,55},
new int[]{90,85,75,115,100,115},
new int[]{115,115,85,90,75,100},
new int[]{100,75,115,90,115,85},
new int[]{50,64,50,45,50,41},
new int[]{70,84,70,65,70,51},
new int[]{100,134,110,95,100,61},
new int[]{106,90,130,90,154,110},
new int[]{106,130,90,110,154,90},
new int[]{100,100,100,100,100,100},
new int[]{40,45,35,65,55,70},
new int[]{50,65,45,85,65,95},
new int[]{70,85,65,105,85,120},
new int[]{45,60,40,70,50,45},
new int[]{60,85,60,85,60,55},
new int[]{80,120,70,110,70,80},
new int[]{50,70,50,50,50,40},
new int[]{70,85,70,60,70,50},
new int[]{100,110,90,85,90,60},
new int[]{35,55,35,30,30,35},
new int[]{70,90,70,60,60,70},
new int[]{38,30,41,30,41,60},
new int[]{78,70,61,50,61,100},
new int[]{45,45,35,20,30,20},
new int[]{50,35,55,25,25,15},
new int[]{60,70,50,90,50,65},
new int[]{50,35,55,25,25,15},
new int[]{60,50,70,50,90,65},
new int[]{40,30,30,40,50,30},
new int[]{60,50,50,60,70,50},
new int[]{80,70,70,90,100,70},
new int[]{40,40,50,30,30,30},
new int[]{70,70,40,60,40,60},
new int[]{90,100,60,90,60,80},
new int[]{40,55,30,30,30,85},
new int[]{60,85,60,50,50,125},
new int[]{40,30,30,55,30,85},
new int[]{60,50,100,85,70,65},
new int[]{28,25,25,45,35,40},
new int[]{38,35,35,65,55,50},
new int[]{68,65,65,125,115,80},
new int[]{40,30,32,50,52,65},
new int[]{70,60,62,80,82,60},
new int[]{60,40,60,40,60,35},
new int[]{60,130,80,60,60,70},
new int[]{60,60,60,35,35,30},
new int[]{80,80,80,55,55,90},
new int[]{150,160,100,95,65,100},
new int[]{31,45,90,30,30,40},
new int[]{61,90,45,50,50,160},
new int[]{1,90,45,30,30,40},
new int[]{64,51,23,51,23,28},
new int[]{84,71,43,71,43,48},
new int[]{104,91,63,91,63,68},
new int[]{72,60,30,20,30,25},
new int[]{144,120,60,40,60,50},
new int[]{50,20,40,20,40,20},
new int[]{30,45,135,45,90,30},
new int[]{50,45,45,35,35,50},
new int[]{70,65,65,55,55,70},
new int[]{50,75,75,65,65,50},
new int[]{50,85,85,55,55,50},
new int[]{50,70,100,40,40,30},
new int[]{60,90,140,50,50,40},
new int[]{70,110,180,60,60,50},
new int[]{30,40,55,40,55,60},
new int[]{60,60,75,60,75,80},
new int[]{40,45,40,65,40,65},
new int[]{70,75,60,105,60,105},
new int[]{60,50,40,85,75,95},
new int[]{60,40,50,75,85,95},
new int[]{65,73,55,47,75,85},
new int[]{65,47,55,73,75,85},
new int[]{50,60,45,100,80,65},
new int[]{70,43,53,43,53,40},
new int[]{100,73,83,73,83,55},
new int[]{45,90,20,65,20,65},
new int[]{70,120,40,95,40,95},
new int[]{130,70,35,70,35,60},
new int[]{170,90,45,90,45,60},
new int[]{60,60,40,65,45,35},
new int[]{70,100,70,105,75,40},
new int[]{70,85,140,85,70,20},
new int[]{60,25,35,70,80,60},
new int[]{80,45,65,90,110,80},
new int[]{60,60,60,60,60,60},
new int[]{45,100,45,45,45,10},
new int[]{50,70,50,50,50,70},
new int[]{80,100,80,80,80,100},
new int[]{50,85,40,85,40,35},
new int[]{70,115,60,115,60,55},
new int[]{45,40,60,40,75,50},
new int[]{75,70,90,70,105,80},
new int[]{73,115,60,60,60,90},
new int[]{73,100,60,100,60,65},
new int[]{70,55,65,95,85,70},
new int[]{70,95,85,55,65,70},
new int[]{50,48,43,46,41,60},
new int[]{110,78,73,76,71,60},
new int[]{43,80,65,50,35,35},
new int[]{63,120,85,90,55,55},
new int[]{40,40,55,40,70,55},
new int[]{60,70,105,70,120,75},
new int[]{66,41,77,61,87,23},
new int[]{86,81,97,81,107,43},
new int[]{45,95,50,40,50,75},
new int[]{75,125,100,70,80,45},
new int[]{20,15,20,10,55,80},
new int[]{95,60,79,100,125,81},
new int[]{70,70,70,70,70,70},
new int[]{60,90,70,60,120,40},
new int[]{44,75,35,63,33,45},
new int[]{64,115,65,83,63,65},
new int[]{20,40,90,30,90,25},
new int[]{40,70,130,60,130,25},
new int[]{99,68,83,72,87,51},
new int[]{65,50,70,95,80,65},
new int[]{65,130,60,75,60,75},
new int[]{95,23,48,23,48,23},
new int[]{50,50,50,50,50,50},
new int[]{80,80,80,80,80,80},
new int[]{70,40,50,55,50,25},
new int[]{90,60,70,75,70,45},
new int[]{110,80,90,95,90,65},
new int[]{35,64,85,74,55,32},
new int[]{55,104,105,94,75,52},
new int[]{55,84,105,114,75,52},
new int[]{100,90,130,45,65,55},
new int[]{43,30,55,40,65,97},
new int[]{45,75,60,40,30,50},
new int[]{65,95,100,60,50,50},
new int[]{95,135,80,110,80,100},
new int[]{40,55,80,35,60,30},
new int[]{60,75,100,55,80,50},
new int[]{80,135,130,95,90,70},
new int[]{80,100,200,50,100,50},
new int[]{80,50,100,100,200,50},
new int[]{80,75,150,75,150,50},
new int[]{80,80,90,110,130,110},
new int[]{80,90,80,130,110,110},
new int[]{100,100,90,150,140,90},
new int[]{100,150,140,100,90,90},
new int[]{105,150,90,150,90,95},
new int[]{100,100,100,100,100,100},
new int[]{50,150,50,150,50,150},
new int[]{55,68,64,45,55,31},
new int[]{75,89,85,55,65,36},
new int[]{95,109,105,75,85,56},
new int[]{44,58,44,58,44,61},
new int[]{64,78,52,78,52,81},
new int[]{76,104,71,104,71,108},
new int[]{53,51,53,61,56,40},
new int[]{64,66,68,81,76,50},
new int[]{84,86,88,111,101,60},
new int[]{40,55,30,30,30,60},
new int[]{55,75,50,40,40,80},
new int[]{85,120,70,50,50,100},
new int[]{59,45,40,35,40,31},
new int[]{79,85,60,55,60,71},
new int[]{37,25,41,25,41,25},
new int[]{77,85,51,55,51,65},
new int[]{45,65,34,40,34,45},
new int[]{60,85,49,60,49,60},
new int[]{80,120,79,95,79,70},
new int[]{40,30,35,50,70,55},
new int[]{60,70,55,125,105,90},
new int[]{67,125,40,30,30,58},
new int[]{97,165,60,65,50,58},
new int[]{30,42,118,42,88,30},
new int[]{60,52,168,47,138,30},
new int[]{40,29,45,29,45,36},
new int[]{60,59,85,79,105,36},
new int[]{70,94,50,94,50,66},
new int[]{30,30,42,30,42,70},
new int[]{70,80,102,80,102,40},
new int[]{60,45,70,45,90,95},
new int[]{55,65,35,60,30,85},
new int[]{85,105,55,85,50,115},
new int[]{45,35,45,62,53,35},
new int[]{70,60,70,87,78,85},
new int[]{76,48,48,57,62,34},
new int[]{111,83,68,92,82,39},
new int[]{75,100,66,60,66,115},
new int[]{90,50,34,60,44,70},
new int[]{150,80,44,90,54,80},
new int[]{55,66,44,44,56,85},
new int[]{65,76,84,54,96,105},
new int[]{60,60,60,105,105,105},
new int[]{100,125,52,105,52,71},
new int[]{49,55,42,42,37,85},
new int[]{71,82,64,64,59,112},
new int[]{45,30,50,65,50,45},
new int[]{63,63,47,41,41,74},
new int[]{103,93,67,71,61,84},
new int[]{57,24,86,24,86,23},
new int[]{67,89,116,79,116,33},
new int[]{50,80,95,10,45,10},
new int[]{20,25,45,70,90,60},
new int[]{100,5,5,15,65,30},
new int[]{76,65,45,92,42,91},
new int[]{50,92,108,92,108,35},
new int[]{58,70,45,40,45,42},
new int[]{68,90,65,50,55,82},
new int[]{108,130,95,80,85,102},
new int[]{135,85,40,40,85,5},
new int[]{40,70,40,35,40,60},
new int[]{70,110,70,115,70,90},
new int[]{68,72,78,38,42,32},
new int[]{108,112,118,68,72,47},
new int[]{40,50,90,30,55,65},
new int[]{70,90,110,60,75,95},
new int[]{48,61,40,61,40,50},
new int[]{83,106,65,86,65,85},
new int[]{74,100,72,90,72,46},
new int[]{49,49,56,49,61,66},
new int[]{69,69,76,69,86,91},
new int[]{45,20,50,60,120,50},
new int[]{60,62,50,62,60,40},
new int[]{90,92,75,92,85,60},
new int[]{70,120,65,45,85,125},
new int[]{70,70,115,130,90,60},
new int[]{110,85,95,80,95,50},
new int[]{115,140,130,55,55,40},
new int[]{100,100,125,110,50,50},
new int[]{75,123,67,95,85,95},
new int[]{75,95,67,125,95,83},
new int[]{85,50,95,120,115,80},
new int[]{86,76,86,116,56,95},
new int[]{65,110,130,60,65,95},
new int[]{65,60,110,130,95,65},
new int[]{75,95,125,45,75,95},
new int[]{110,130,80,70,60,80},
new int[]{85,80,70,135,75,90},
new int[]{68,125,65,65,115,80},
new int[]{60,55,145,75,150,40},
new int[]{45,100,135,65,135,45},
new int[]{70,80,70,80,70,110},
new int[]{50,50,77,95,77,91},
new int[]{75,75,130,75,130,95},
new int[]{80,105,105,105,105,80},
new int[]{75,125,70,125,70,115},
new int[]{100,120,120,150,100,90},
new int[]{90,120,100,150,120,100},
new int[]{91,90,106,130,106,77},
new int[]{110,160,110,80,110,100},
new int[]{150,100,120,100,120,90},
new int[]{120,70,120,75,130,85},
new int[]{80,80,80,80,80,80},
new int[]{100,100,100,100,100,100},
new int[]{70,90,90,135,90,125},
new int[]{100,100,100,100,100,100},
new int[]{120,120,120,120,120,120}
    };

        public static DataTable SpeciesTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("Species", typeof(int));
            table.Columns.Add("EXP Growth", typeof(int));

            table.Rows.Add(0, 0);
            table.Rows.Add(1, 3);
            table.Rows.Add(2, 3);
            table.Rows.Add(3, 3);
            table.Rows.Add(4, 3);
            table.Rows.Add(5, 3);
            table.Rows.Add(6, 3);
            table.Rows.Add(7, 3);
            table.Rows.Add(8, 3);
            table.Rows.Add(9, 3);
            table.Rows.Add(10, 2);
            table.Rows.Add(11, 2);
            table.Rows.Add(12, 2);
            table.Rows.Add(13, 2);
            table.Rows.Add(14, 2);
            table.Rows.Add(15, 2);
            table.Rows.Add(16, 3);
            table.Rows.Add(17, 3);
            table.Rows.Add(18, 3);
            table.Rows.Add(19, 2);
            table.Rows.Add(20, 2);
            table.Rows.Add(21, 2);
            table.Rows.Add(22, 2);
            table.Rows.Add(23, 2);
            table.Rows.Add(24, 2);
            table.Rows.Add(25, 2);
            table.Rows.Add(26, 2);
            table.Rows.Add(27, 2);
            table.Rows.Add(28, 2);
            table.Rows.Add(29, 3);
            table.Rows.Add(30, 3);
            table.Rows.Add(31, 3);
            table.Rows.Add(32, 3);
            table.Rows.Add(33, 3);
            table.Rows.Add(34, 3);
            table.Rows.Add(35, 1);
            table.Rows.Add(36, 1);
            table.Rows.Add(37, 2);
            table.Rows.Add(38, 2);
            table.Rows.Add(39, 1);
            table.Rows.Add(40, 1);
            table.Rows.Add(41, 2);
            table.Rows.Add(42, 2);
            table.Rows.Add(43, 3);
            table.Rows.Add(44, 3);
            table.Rows.Add(45, 3);
            table.Rows.Add(46, 2);
            table.Rows.Add(47, 2);
            table.Rows.Add(48, 2);
            table.Rows.Add(49, 2);
            table.Rows.Add(50, 2);
            table.Rows.Add(51, 2);
            table.Rows.Add(52, 2);
            table.Rows.Add(53, 2);
            table.Rows.Add(54, 2);
            table.Rows.Add(55, 2);
            table.Rows.Add(56, 2);
            table.Rows.Add(57, 2);
            table.Rows.Add(58, 4);
            table.Rows.Add(59, 4);
            table.Rows.Add(60, 3);
            table.Rows.Add(61, 3);
            table.Rows.Add(62, 3);
            table.Rows.Add(63, 3);
            table.Rows.Add(64, 3);
            table.Rows.Add(65, 3);
            table.Rows.Add(66, 3);
            table.Rows.Add(67, 3);
            table.Rows.Add(68, 3);
            table.Rows.Add(69, 3);
            table.Rows.Add(70, 3);
            table.Rows.Add(71, 3);
            table.Rows.Add(72, 4);
            table.Rows.Add(73, 4);
            table.Rows.Add(74, 3);
            table.Rows.Add(75, 3);
            table.Rows.Add(76, 3);
            table.Rows.Add(77, 2);
            table.Rows.Add(78, 2);
            table.Rows.Add(79, 2);
            table.Rows.Add(80, 2);
            table.Rows.Add(81, 2);
            table.Rows.Add(82, 2);
            table.Rows.Add(83, 2);
            table.Rows.Add(84, 2);
            table.Rows.Add(85, 2);
            table.Rows.Add(86, 2);
            table.Rows.Add(87, 2);
            table.Rows.Add(88, 2);
            table.Rows.Add(89, 2);
            table.Rows.Add(90, 4);
            table.Rows.Add(91, 4);
            table.Rows.Add(92, 3);
            table.Rows.Add(93, 3);
            table.Rows.Add(94, 3);
            table.Rows.Add(95, 2);
            table.Rows.Add(96, 2);
            table.Rows.Add(97, 2);
            table.Rows.Add(98, 2);
            table.Rows.Add(99, 2);
            table.Rows.Add(100, 2);
            table.Rows.Add(101, 2);
            table.Rows.Add(102, 4);
            table.Rows.Add(103, 4);
            table.Rows.Add(104, 2);
            table.Rows.Add(105, 2);
            table.Rows.Add(106, 2);
            table.Rows.Add(107, 2);
            table.Rows.Add(108, 2);
            table.Rows.Add(109, 2);
            table.Rows.Add(110, 2);
            table.Rows.Add(111, 4);
            table.Rows.Add(112, 4);
            table.Rows.Add(113, 1);
            table.Rows.Add(114, 2);
            table.Rows.Add(115, 2);
            table.Rows.Add(116, 2);
            table.Rows.Add(117, 2);
            table.Rows.Add(118, 2);
            table.Rows.Add(119, 2);
            table.Rows.Add(120, 4);
            table.Rows.Add(121, 4);
            table.Rows.Add(122, 2);
            table.Rows.Add(123, 2);
            table.Rows.Add(124, 2);
            table.Rows.Add(125, 2);
            table.Rows.Add(126, 2);
            table.Rows.Add(127, 4);
            table.Rows.Add(128, 4);
            table.Rows.Add(129, 4);
            table.Rows.Add(130, 4);
            table.Rows.Add(131, 4);
            table.Rows.Add(132, 2);
            table.Rows.Add(133, 2);
            table.Rows.Add(134, 2);
            table.Rows.Add(135, 2);
            table.Rows.Add(136, 2);
            table.Rows.Add(137, 2);
            table.Rows.Add(138, 2);
            table.Rows.Add(139, 2);
            table.Rows.Add(140, 2);
            table.Rows.Add(141, 2);
            table.Rows.Add(142, 4);
            table.Rows.Add(143, 4);
            table.Rows.Add(144, 4);
            table.Rows.Add(145, 4);
            table.Rows.Add(146, 4);
            table.Rows.Add(147, 4);
            table.Rows.Add(148, 4);
            table.Rows.Add(149, 4);
            table.Rows.Add(150, 4);
            table.Rows.Add(151, 3);
            table.Rows.Add(152, 3);
            table.Rows.Add(153, 3);
            table.Rows.Add(154, 3);
            table.Rows.Add(155, 3);
            table.Rows.Add(156, 3);
            table.Rows.Add(157, 3);
            table.Rows.Add(158, 3);
            table.Rows.Add(159, 3);
            table.Rows.Add(160, 3);
            table.Rows.Add(161, 2);
            table.Rows.Add(162, 2);
            table.Rows.Add(163, 2);
            table.Rows.Add(164, 2);
            table.Rows.Add(165, 1);
            table.Rows.Add(166, 1);
            table.Rows.Add(167, 1);
            table.Rows.Add(168, 1);
            table.Rows.Add(169, 2);
            table.Rows.Add(170, 4);
            table.Rows.Add(171, 4);
            table.Rows.Add(172, 2);
            table.Rows.Add(173, 1);
            table.Rows.Add(174, 1);
            table.Rows.Add(175, 1);
            table.Rows.Add(176, 1);
            table.Rows.Add(177, 2);
            table.Rows.Add(178, 2);
            table.Rows.Add(179, 3);
            table.Rows.Add(180, 3);
            table.Rows.Add(181, 3);
            table.Rows.Add(182, 3);
            table.Rows.Add(183, 1);
            table.Rows.Add(184, 1);
            table.Rows.Add(185, 2);
            table.Rows.Add(186, 3);
            table.Rows.Add(187, 3);
            table.Rows.Add(188, 3);
            table.Rows.Add(189, 3);
            table.Rows.Add(190, 1);
            table.Rows.Add(191, 3);
            table.Rows.Add(192, 3);
            table.Rows.Add(193, 2);
            table.Rows.Add(194, 2);
            table.Rows.Add(195, 2);
            table.Rows.Add(196, 2);
            table.Rows.Add(197, 2);
            table.Rows.Add(198, 3);
            table.Rows.Add(199, 2);
            table.Rows.Add(200, 1);
            table.Rows.Add(201, 2);
            table.Rows.Add(202, 2);
            table.Rows.Add(203, 2);
            table.Rows.Add(204, 2);
            table.Rows.Add(205, 2);
            table.Rows.Add(206, 2);
            table.Rows.Add(207, 3);
            table.Rows.Add(208, 2);
            table.Rows.Add(209, 1);
            table.Rows.Add(210, 1);
            table.Rows.Add(211, 2);
            table.Rows.Add(212, 2);
            table.Rows.Add(213, 3);
            table.Rows.Add(214, 4);
            table.Rows.Add(215, 3);
            table.Rows.Add(216, 2);
            table.Rows.Add(217, 2);
            table.Rows.Add(218, 2);
            table.Rows.Add(219, 2);
            table.Rows.Add(220, 4);
            table.Rows.Add(221, 4);
            table.Rows.Add(222, 1);
            table.Rows.Add(223, 2);
            table.Rows.Add(224, 2);
            table.Rows.Add(225, 1);
            table.Rows.Add(226, 4);
            table.Rows.Add(227, 4);
            table.Rows.Add(228, 4);
            table.Rows.Add(229, 4);
            table.Rows.Add(230, 2);
            table.Rows.Add(231, 2);
            table.Rows.Add(232, 2);
            table.Rows.Add(233, 2);
            table.Rows.Add(234, 4);
            table.Rows.Add(235, 1);
            table.Rows.Add(236, 2);
            table.Rows.Add(237, 2);
            table.Rows.Add(238, 2);
            table.Rows.Add(239, 2);
            table.Rows.Add(240, 2);
            table.Rows.Add(241, 4);
            table.Rows.Add(242, 1);
            table.Rows.Add(243, 4);
            table.Rows.Add(244, 4);
            table.Rows.Add(245, 4);
            table.Rows.Add(246, 4);
            table.Rows.Add(247, 4);
            table.Rows.Add(248, 4);
            table.Rows.Add(249, 4);
            table.Rows.Add(250, 4);
            table.Rows.Add(251, 3);
            table.Rows.Add(252, 3);
            table.Rows.Add(253, 3);
            table.Rows.Add(254, 3);
            table.Rows.Add(255, 3);
            table.Rows.Add(256, 3);
            table.Rows.Add(257, 3);
            table.Rows.Add(258, 3);
            table.Rows.Add(259, 3);
            table.Rows.Add(260, 3);
            table.Rows.Add(261, 2);
            table.Rows.Add(262, 2);
            table.Rows.Add(263, 2);
            table.Rows.Add(264, 2);
            table.Rows.Add(265, 2);
            table.Rows.Add(266, 2);
            table.Rows.Add(267, 2);
            table.Rows.Add(268, 2);
            table.Rows.Add(269, 2);
            table.Rows.Add(270, 3);
            table.Rows.Add(271, 3);
            table.Rows.Add(272, 3);
            table.Rows.Add(273, 3);
            table.Rows.Add(274, 3);
            table.Rows.Add(275, 3);
            table.Rows.Add(276, 3);
            table.Rows.Add(277, 3);
            table.Rows.Add(278, 2);
            table.Rows.Add(279, 2);
            table.Rows.Add(280, 4);
            table.Rows.Add(281, 4);
            table.Rows.Add(282, 4);
            table.Rows.Add(283, 2);
            table.Rows.Add(284, 2);
            table.Rows.Add(285, 5);
            table.Rows.Add(286, 5);
            table.Rows.Add(287, 4);
            table.Rows.Add(288, 4);
            table.Rows.Add(289, 4);
            table.Rows.Add(290, 0);
            table.Rows.Add(291, 0);
            table.Rows.Add(292, 0);
            table.Rows.Add(293, 3);
            table.Rows.Add(294, 3);
            table.Rows.Add(295, 3);
            table.Rows.Add(296, 5);
            table.Rows.Add(297, 5);
            table.Rows.Add(298, 1);
            table.Rows.Add(299, 2);
            table.Rows.Add(300, 1);
            table.Rows.Add(301, 1);
            table.Rows.Add(302, 3);
            table.Rows.Add(303, 1);
            table.Rows.Add(304, 4);
            table.Rows.Add(305, 4);
            table.Rows.Add(306, 4);
            table.Rows.Add(307, 2);
            table.Rows.Add(308, 2);
            table.Rows.Add(309, 4);
            table.Rows.Add(310, 4);
            table.Rows.Add(311, 2);
            table.Rows.Add(312, 2);
            table.Rows.Add(313, 0);
            table.Rows.Add(314, 5);
            table.Rows.Add(315, 3);
            table.Rows.Add(316, 5);
            table.Rows.Add(317, 5);
            table.Rows.Add(318, 4);
            table.Rows.Add(319, 4);
            table.Rows.Add(320, 5);
            table.Rows.Add(321, 5);
            table.Rows.Add(322, 2);
            table.Rows.Add(323, 2);
            table.Rows.Add(324, 2);
            table.Rows.Add(325, 1);
            table.Rows.Add(326, 1);
            table.Rows.Add(327, 1);
            table.Rows.Add(328, 3);
            table.Rows.Add(329, 3);
            table.Rows.Add(330, 3);
            table.Rows.Add(331, 3);
            table.Rows.Add(332, 3);
            table.Rows.Add(333, 0);
            table.Rows.Add(334, 0);
            table.Rows.Add(335, 0);
            table.Rows.Add(336, 5);
            table.Rows.Add(337, 1);
            table.Rows.Add(338, 1);
            table.Rows.Add(339, 2);
            table.Rows.Add(340, 2);
            table.Rows.Add(341, 5);
            table.Rows.Add(342, 5);
            table.Rows.Add(343, 2);
            table.Rows.Add(344, 2);
            table.Rows.Add(345, 0);
            table.Rows.Add(346, 0);
            table.Rows.Add(347, 0);
            table.Rows.Add(348, 0);
            table.Rows.Add(349, 0);
            table.Rows.Add(350, 0);
            table.Rows.Add(351, 2);
            table.Rows.Add(352, 3);
            table.Rows.Add(353, 1);
            table.Rows.Add(354, 1);
            table.Rows.Add(355, 1);
            table.Rows.Add(356, 1);
            table.Rows.Add(357, 4);
            table.Rows.Add(358, 1);
            table.Rows.Add(359, 3);
            table.Rows.Add(360, 2);
            table.Rows.Add(361, 2);
            table.Rows.Add(362, 2);
            table.Rows.Add(363, 3);
            table.Rows.Add(364, 3);
            table.Rows.Add(365, 3);
            table.Rows.Add(366, 0);
            table.Rows.Add(367, 0);
            table.Rows.Add(368, 0);
            table.Rows.Add(369, 4);
            table.Rows.Add(370, 1);
            table.Rows.Add(371, 4);
            table.Rows.Add(372, 4);
            table.Rows.Add(373, 4);
            table.Rows.Add(374, 4);
            table.Rows.Add(375, 4);
            table.Rows.Add(376, 4);
            table.Rows.Add(377, 4);
            table.Rows.Add(378, 4);
            table.Rows.Add(379, 4);
            table.Rows.Add(380, 4);
            table.Rows.Add(381, 4);
            table.Rows.Add(382, 4);
            table.Rows.Add(383, 4);
            table.Rows.Add(384, 4);
            table.Rows.Add(385, 4);
            table.Rows.Add(386, 4);
            table.Rows.Add(387, 3);
            table.Rows.Add(388, 3);
            table.Rows.Add(389, 3);
            table.Rows.Add(390, 3);
            table.Rows.Add(391, 3);
            table.Rows.Add(392, 3);
            table.Rows.Add(393, 3);
            table.Rows.Add(394, 3);
            table.Rows.Add(395, 3);
            table.Rows.Add(396, 3);
            table.Rows.Add(397, 3);
            table.Rows.Add(398, 3);
            table.Rows.Add(399, 2);
            table.Rows.Add(400, 2);
            table.Rows.Add(401, 3);
            table.Rows.Add(402, 3);
            table.Rows.Add(403, 3);
            table.Rows.Add(404, 3);
            table.Rows.Add(405, 3);
            table.Rows.Add(406, 3);
            table.Rows.Add(407, 3);
            table.Rows.Add(408, 0);
            table.Rows.Add(409, 0);
            table.Rows.Add(410, 0);
            table.Rows.Add(411, 0);
            table.Rows.Add(412, 2);
            table.Rows.Add(413, 2);
            table.Rows.Add(414, 2);
            table.Rows.Add(415, 3);
            table.Rows.Add(416, 3);
            table.Rows.Add(417, 2);
            table.Rows.Add(418, 2);
            table.Rows.Add(419, 2);
            table.Rows.Add(420, 2);
            table.Rows.Add(421, 2);
            table.Rows.Add(422, 2);
            table.Rows.Add(423, 2);
            table.Rows.Add(424, 1);
            table.Rows.Add(425, 5);
            table.Rows.Add(426, 5);
            table.Rows.Add(427, 2);
            table.Rows.Add(428, 2);
            table.Rows.Add(429, 1);
            table.Rows.Add(430, 3);
            table.Rows.Add(431, 1);
            table.Rows.Add(432, 1);
            table.Rows.Add(433, 1);
            table.Rows.Add(434, 2);
            table.Rows.Add(435, 2);
            table.Rows.Add(436, 2);
            table.Rows.Add(437, 2);
            table.Rows.Add(438, 2);
            table.Rows.Add(439, 2);
            table.Rows.Add(440, 1);
            table.Rows.Add(441, 3);
            table.Rows.Add(442, 2);
            table.Rows.Add(443, 4);
            table.Rows.Add(444, 4);
            table.Rows.Add(445, 4);
            table.Rows.Add(446, 4);
            table.Rows.Add(447, 3);
            table.Rows.Add(448, 3);
            table.Rows.Add(449, 4);
            table.Rows.Add(450, 4);
            table.Rows.Add(451, 4);
            table.Rows.Add(452, 4);
            table.Rows.Add(453, 2);
            table.Rows.Add(454, 2);
            table.Rows.Add(455, 4);
            table.Rows.Add(456, 0);
            table.Rows.Add(457, 0);
            table.Rows.Add(458, 4);
            table.Rows.Add(459, 4);
            table.Rows.Add(460, 4);
            table.Rows.Add(461, 3);
            table.Rows.Add(462, 2);
            table.Rows.Add(463, 2);
            table.Rows.Add(464, 4);
            table.Rows.Add(465, 2);
            table.Rows.Add(466, 2);
            table.Rows.Add(467, 2);
            table.Rows.Add(468, 1);
            table.Rows.Add(469, 2);
            table.Rows.Add(470, 2);
            table.Rows.Add(471, 2);
            table.Rows.Add(472, 3);
            table.Rows.Add(473, 4);
            table.Rows.Add(474, 2);
            table.Rows.Add(475, 4);
            table.Rows.Add(476, 2);
            table.Rows.Add(477, 1);
            table.Rows.Add(478, 2);
            table.Rows.Add(479, 2);
            table.Rows.Add(480, 4);
            table.Rows.Add(481, 4);
            table.Rows.Add(482, 4);
            table.Rows.Add(483, 4);
            table.Rows.Add(484, 4);
            table.Rows.Add(485, 4);
            table.Rows.Add(486, 4);
            table.Rows.Add(487, 4);
            table.Rows.Add(488, 4);
            table.Rows.Add(489, 4);
            table.Rows.Add(490, 4);
            table.Rows.Add(491, 4);
            table.Rows.Add(492, 3);
            table.Rows.Add(493, 4);

            return table;
        }

        internal static DataTable ExpTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("Level", typeof(byte));
            table.Columns.Add("0 - Erratic", typeof(uint));
            table.Columns.Add("1 - Fast", typeof(uint));
            table.Columns.Add("2 - MF", typeof(uint));
            table.Columns.Add("3 - MS", typeof(uint));
            table.Columns.Add("4 - Slow", typeof(uint));
            table.Columns.Add("5 - Fluctuating", typeof(uint));
            table.Rows.Add(0, 0, 0, 0, 0, 0, 0);
            table.Rows.Add(1, 0, 0, 0, 0, 0, 0);
            table.Rows.Add(2, 15, 6, 8, 9, 10, 4);
            table.Rows.Add(3, 52, 21, 27, 57, 33, 13);
            table.Rows.Add(4, 122, 51, 64, 96, 80, 32);
            table.Rows.Add(5, 237, 100, 125, 135, 156, 65);
            table.Rows.Add(6, 406, 172, 216, 179, 270, 112);
            table.Rows.Add(7, 637, 274, 343, 236, 428, 178);
            table.Rows.Add(8, 942, 409, 512, 314, 640, 276);
            table.Rows.Add(9, 1326, 583, 729, 419, 911, 393);
            table.Rows.Add(10, 1800, 800, 1000, 560, 1250, 540);
            table.Rows.Add(11, 2369, 1064, 1331, 742, 1663, 745);
            table.Rows.Add(12, 3041, 1382, 1728, 973, 2160, 967);
            table.Rows.Add(13, 3822, 1757, 2197, 1261, 2746, 1230);
            table.Rows.Add(14, 4719, 2195, 2744, 1612, 3430, 1591);
            table.Rows.Add(15, 5737, 2700, 3375, 2035, 4218, 1957);
            table.Rows.Add(16, 6881, 3276, 4096, 2535, 5120, 2457);
            table.Rows.Add(17, 8155, 3930, 4913, 3120, 6141, 3046);
            table.Rows.Add(18, 9564, 4665, 5832, 3798, 7290, 3732);
            table.Rows.Add(19, 11111, 5487, 6859, 4575, 8573, 4526);
            table.Rows.Add(20, 12800, 6400, 8000, 5460, 10000, 5440);
            table.Rows.Add(21, 14632, 7408, 9261, 6458, 11576, 6482);
            table.Rows.Add(22, 16610, 8518, 10648, 7577, 13310, 7666);
            table.Rows.Add(23, 18737, 9733, 12167, 8825, 15208, 9003);
            table.Rows.Add(24, 21012, 11059, 13824, 10208, 17280, 10506);
            table.Rows.Add(25, 23437, 12500, 15625, 11735, 19531, 12187);
            table.Rows.Add(26, 26012, 14060, 17576, 13411, 21970, 14060);
            table.Rows.Add(27, 28737, 15746, 19683, 15244, 24603, 16140);
            table.Rows.Add(28, 31610, 17561, 21952, 17242, 27440, 18439);
            table.Rows.Add(29, 34632, 19511, 24389, 19411, 30486, 20974);
            table.Rows.Add(30, 37800, 21600, 27000, 21760, 33750, 23760);
            table.Rows.Add(31, 41111, 23832, 29791, 24294, 37238, 26811);
            table.Rows.Add(32, 44564, 26214, 32768, 27021, 40960, 30146);
            table.Rows.Add(33, 48155, 28749, 35937, 29949, 44921, 33780);
            table.Rows.Add(34, 51881, 31443, 39304, 33084, 49130, 37731);
            table.Rows.Add(35, 55737, 34300, 42875, 36435, 53593, 42017);
            table.Rows.Add(36, 59719, 37324, 46656, 40007, 58320, 46656);
            table.Rows.Add(37, 63822, 40522, 50653, 43808, 63316, 50653);
            table.Rows.Add(38, 68041, 43897, 54872, 47846, 68590, 55969);
            table.Rows.Add(39, 72369, 47455, 59319, 52127, 74148, 60505);
            table.Rows.Add(40, 76800, 51200, 64000, 56660, 80000, 66560);
            table.Rows.Add(41, 81326, 55136, 68921, 61450, 86151, 71677);
            table.Rows.Add(42, 85942, 59270, 74088, 66505, 92610, 78533);
            table.Rows.Add(43, 90637, 63605, 79507, 71833, 99383, 84277);
            table.Rows.Add(44, 95406, 68147, 85184, 77440, 106480, 91998);
            table.Rows.Add(45, 100237, 72900, 91125, 83335, 113906, 98415);
            table.Rows.Add(46, 105122, 77868, 97336, 89523, 121670, 107069);
            table.Rows.Add(47, 110052, 83058, 103823, 96012, 129778, 114205);
            table.Rows.Add(48, 115015, 88473, 110592, 102810, 138240, 123863);
            table.Rows.Add(49, 120001, 94119, 117649, 109923, 147061, 131766);
            table.Rows.Add(50, 125000, 100000, 125000, 117360, 156250, 142500);
            table.Rows.Add(51, 131324, 106120, 132651, 125126, 165813, 151222);
            table.Rows.Add(52, 137795, 112486, 140608, 133229, 175760, 163105);
            table.Rows.Add(53, 144410, 119101, 148877, 141677, 186096, 172697);
            table.Rows.Add(54, 151165, 125971, 157464, 150476, 196830, 185807);
            table.Rows.Add(55, 158056, 133100, 166375, 159635, 207968, 196322);
            table.Rows.Add(56, 165079, 140492, 175616, 169159, 219520, 210739);
            table.Rows.Add(57, 172229, 148154, 185193, 179056, 231491, 222231);
            table.Rows.Add(58, 179503, 156089, 195112, 189334, 243890, 238036);
            table.Rows.Add(59, 186894, 164303, 205379, 199999, 256723, 250562);
            table.Rows.Add(60, 194400, 172800, 216000, 211060, 270000, 267840);
            table.Rows.Add(61, 202013, 181584, 226981, 222522, 283726, 281456);
            table.Rows.Add(62, 209728, 190662, 238328, 234393, 297910, 300293);
            table.Rows.Add(63, 217540, 200037, 250047, 246681, 312558, 315059);
            table.Rows.Add(64, 225443, 209715, 262144, 259392, 327680, 335544);
            table.Rows.Add(65, 233431, 219700, 274625, 272535, 343281, 351520);
            table.Rows.Add(66, 241496, 229996, 287496, 286115, 359370, 373744);
            table.Rows.Add(67, 249633, 240610, 300763, 300140, 375953, 390991);
            table.Rows.Add(68, 257834, 251545, 314432, 314618, 393040, 415050);
            table.Rows.Add(69, 267406, 262807, 328509, 329555, 410636, 433631);
            table.Rows.Add(70, 276458, 274400, 343000, 344960, 428750, 459620);
            table.Rows.Add(71, 286328, 286328, 357911, 360838, 447388, 479600);
            table.Rows.Add(72, 296358, 298598, 373248, 377197, 466560, 507617);
            table.Rows.Add(73, 305767, 311213, 389017, 394045, 486271, 529063);
            table.Rows.Add(74, 316074, 324179, 405224, 411388, 506530, 559209);
            table.Rows.Add(75, 326531, 337500, 421875, 429235, 527343, 582187);
            table.Rows.Add(76, 336255, 351180, 438976, 447591, 548720, 614566);
            table.Rows.Add(77, 346965, 365226, 456533, 466464, 570666, 639146);
            table.Rows.Add(78, 357812, 379641, 474552, 485862, 593190, 673863);
            table.Rows.Add(79, 367807, 394431, 493039, 505791, 616298, 700115);
            table.Rows.Add(80, 378880, 409600, 512000, 526260, 640000, 737280);
            table.Rows.Add(81, 390077, 425152, 531441, 547274, 664301, 765275);
            table.Rows.Add(82, 400293, 441094, 551368, 568841, 689210, 804997);
            table.Rows.Add(83, 411686, 457429, 571787, 590969, 714733, 834809);
            table.Rows.Add(84, 423190, 474163, 592704, 613664, 740880, 877201);
            table.Rows.Add(85, 433572, 491300, 614125, 636935, 767656, 908905);
            table.Rows.Add(86, 445239, 508844, 636056, 660787, 795070, 954084);
            table.Rows.Add(87, 457001, 526802, 658503, 685228, 823128, 987754);
            table.Rows.Add(88, 467489, 545177, 681472, 710266, 851840, 1035837);
            table.Rows.Add(89, 479378, 563975, 704969, 735907, 881211, 1071552);
            table.Rows.Add(90, 491346, 583200, 729000, 762160, 911250, 1122660);
            table.Rows.Add(91, 501878, 602856, 753571, 789030, 941963, 1160499);
            table.Rows.Add(92, 513934, 622950, 778688, 816525, 973360, 1214753);
            table.Rows.Add(93, 526049, 643485, 804357, 844653, 1005446, 1254796);
            table.Rows.Add(94, 536557, 664467, 830584, 873420, 1038230, 1312322);
            table.Rows.Add(95, 548720, 685900, 857375, 902835, 1071718, 1354652);
            table.Rows.Add(96, 560922, 707788, 884736, 932903, 1105920, 1415577);
            table.Rows.Add(97, 571333, 730138, 912673, 963632, 1140841, 1460276);
            table.Rows.Add(98, 583539, 752953, 941192, 995030, 1176490, 1524731);
            table.Rows.Add(99, 591882, 776239, 970299, 1027103, 1212873, 1571884);
            table.Rows.Add(100, 600000, 800000, 1000000, 1059860, 1250000, 1640000);
            return table;
        }

        static DataTable Char4to5()
        {
            // Converted from NebuK's Python Implementation SQL Database
            // http://projectpokemon.org/forums/showthread.php?14875
            // http://nopaste.ghostdub.de/?306
            DataTable table = new DataTable();
            table.Columns.Add("Old", typeof(int));
            table.Columns.Add("New", typeof(int));
            table.Columns.Add("Symbol", typeof(char));

            DataColumn[] keyColumns = new DataColumn[1];
            keyColumns[0] = table.Columns["Old"]; // table.Rows.Find(val)[1] will look in the "Old" column, and return the "New" value.
            table.PrimaryKey = keyColumns;
            #region Old-New/Symbol Adding Entries
            table.Rows.Add(1, 12288, '　');
            table.Rows.Add(2, 12353, 'ぁ');
            table.Rows.Add(3, 12354, 'あ');
            table.Rows.Add(4, 12355, 'ぃ');
            table.Rows.Add(5, 12356, 'い');
            table.Rows.Add(6, 12357, 'ぅ');
            table.Rows.Add(7, 12358, 'う');
            table.Rows.Add(8, 12359, 'ぇ');
            table.Rows.Add(9, 12360, 'え');
            table.Rows.Add(10, 12361, 'ぉ');
            table.Rows.Add(11, 12362, 'お');
            table.Rows.Add(12, 12363, 'か');
            table.Rows.Add(13, 12364, 'が');
            table.Rows.Add(14, 12365, 'き');
            table.Rows.Add(15, 12366, 'ぎ');
            table.Rows.Add(16, 12367, 'く');
            table.Rows.Add(17, 12368, 'ぐ');
            table.Rows.Add(18, 12369, 'け');
            table.Rows.Add(19, 12370, 'げ');
            table.Rows.Add(20, 12371, 'こ');
            table.Rows.Add(21, 12372, 'ご');
            table.Rows.Add(22, 12373, 'さ');
            table.Rows.Add(23, 12374, 'ざ');
            table.Rows.Add(24, 12375, 'し');
            table.Rows.Add(25, 12376, 'じ');
            table.Rows.Add(26, 12377, 'す');
            table.Rows.Add(27, 12378, 'ず');
            table.Rows.Add(28, 12379, 'せ');
            table.Rows.Add(29, 12380, 'ぜ');
            table.Rows.Add(30, 12381, 'そ');
            table.Rows.Add(31, 12382, 'ぞ');
            table.Rows.Add(32, 12383, 'た');
            table.Rows.Add(33, 12384, 'だ');
            table.Rows.Add(34, 12385, 'ち');
            table.Rows.Add(35, 12386, 'ぢ');
            table.Rows.Add(36, 12387, 'っ');
            table.Rows.Add(37, 12388, 'つ');
            table.Rows.Add(38, 12389, 'づ');
            table.Rows.Add(39, 12390, 'て');
            table.Rows.Add(40, 12391, 'で');
            table.Rows.Add(41, 12392, 'と');
            table.Rows.Add(42, 12393, 'ど');
            table.Rows.Add(43, 12394, 'な');
            table.Rows.Add(44, 12395, 'に');
            table.Rows.Add(45, 12396, 'ぬ');
            table.Rows.Add(46, 12397, 'ね');
            table.Rows.Add(47, 12398, 'の');
            table.Rows.Add(48, 12399, 'は');
            table.Rows.Add(49, 12400, 'ば');
            table.Rows.Add(50, 12401, 'ぱ');
            table.Rows.Add(51, 12402, 'ひ');
            table.Rows.Add(52, 12403, 'び');
            table.Rows.Add(53, 12404, 'ぴ');
            table.Rows.Add(54, 12405, 'ふ');
            table.Rows.Add(55, 12406, 'ぶ');
            table.Rows.Add(56, 12407, 'ぷ');
            table.Rows.Add(57, 12408, 'へ');
            table.Rows.Add(58, 12409, 'べ');
            table.Rows.Add(59, 12410, 'ぺ');
            table.Rows.Add(60, 12411, 'ほ');
            table.Rows.Add(61, 12412, 'ぼ');
            table.Rows.Add(62, 12413, 'ぽ');
            table.Rows.Add(63, 12414, 'ま');
            table.Rows.Add(64, 12415, 'み');
            table.Rows.Add(65, 12416, 'む');
            table.Rows.Add(66, 12417, 'め');
            table.Rows.Add(67, 12418, 'も');
            table.Rows.Add(68, 12419, 'ゃ');
            table.Rows.Add(69, 12420, 'や');
            table.Rows.Add(70, 12421, 'ゅ');
            table.Rows.Add(71, 12422, 'ゆ');
            table.Rows.Add(72, 12423, 'ょ');
            table.Rows.Add(73, 12424, 'よ');
            table.Rows.Add(74, 12425, 'ら');
            table.Rows.Add(75, 12426, 'り');
            table.Rows.Add(76, 12427, 'る');
            table.Rows.Add(77, 12428, 'れ');
            table.Rows.Add(78, 12429, 'ろ');
            table.Rows.Add(79, 12431, 'わ');
            table.Rows.Add(80, 12434, 'を');
            table.Rows.Add(81, 12435, 'ん');
            table.Rows.Add(82, 12449, 'ァ');
            table.Rows.Add(83, 12450, 'ア');
            table.Rows.Add(84, 12451, 'ィ');
            table.Rows.Add(85, 12452, 'イ');
            table.Rows.Add(86, 12453, 'ゥ');
            table.Rows.Add(87, 12454, 'ウ');
            table.Rows.Add(88, 12455, 'ェ');
            table.Rows.Add(89, 12456, 'エ');
            table.Rows.Add(90, 12457, 'ォ');
            table.Rows.Add(91, 12458, 'オ');
            table.Rows.Add(92, 12459, 'カ');
            table.Rows.Add(93, 12460, 'ガ');
            table.Rows.Add(94, 12461, 'キ');
            table.Rows.Add(95, 12462, 'ギ');
            table.Rows.Add(96, 12463, 'ク');
            table.Rows.Add(97, 12464, 'グ');
            table.Rows.Add(98, 12465, 'ケ');
            table.Rows.Add(99, 12466, 'ゲ');
            table.Rows.Add(100, 12467, 'コ');
            table.Rows.Add(101, 12468, 'ゴ');
            table.Rows.Add(102, 12469, 'サ');
            table.Rows.Add(103, 12470, 'ザ');
            table.Rows.Add(104, 12471, 'シ');
            table.Rows.Add(105, 12472, 'ジ');
            table.Rows.Add(106, 12473, 'ス');
            table.Rows.Add(107, 12474, 'ズ');
            table.Rows.Add(108, 12475, 'セ');
            table.Rows.Add(109, 12476, 'ゼ');
            table.Rows.Add(110, 12477, 'ソ');
            table.Rows.Add(111, 12478, 'ゾ');
            table.Rows.Add(112, 12479, 'タ');
            table.Rows.Add(113, 12480, 'ダ');
            table.Rows.Add(114, 12481, 'チ');
            table.Rows.Add(115, 12482, 'ヂ');
            table.Rows.Add(116, 12483, 'ッ');
            table.Rows.Add(117, 12484, 'ツ');
            table.Rows.Add(118, 12485, 'ヅ');
            table.Rows.Add(119, 12486, 'テ');
            table.Rows.Add(120, 12487, 'デ');
            table.Rows.Add(121, 12488, 'ト');
            table.Rows.Add(122, 12489, 'ド');
            table.Rows.Add(123, 12490, 'ナ');
            table.Rows.Add(124, 12491, 'ニ');
            table.Rows.Add(125, 12492, 'ヌ');
            table.Rows.Add(126, 12493, 'ネ');
            table.Rows.Add(127, 12494, 'ノ');
            table.Rows.Add(128, 12495, 'ハ');
            table.Rows.Add(129, 12496, 'バ');
            table.Rows.Add(130, 12497, 'パ');
            table.Rows.Add(131, 12498, 'ヒ');
            table.Rows.Add(132, 12499, 'ビ');
            table.Rows.Add(133, 12500, 'ピ');
            table.Rows.Add(134, 12501, 'フ');
            table.Rows.Add(135, 12502, 'ブ');
            table.Rows.Add(136, 12503, 'プ');
            table.Rows.Add(137, 12504, 'ヘ');
            table.Rows.Add(138, 12505, 'ベ');
            table.Rows.Add(139, 12506, 'ペ');
            table.Rows.Add(140, 12507, 'ホ');
            table.Rows.Add(141, 12508, 'ボ');
            table.Rows.Add(142, 12509, 'ポ');
            table.Rows.Add(143, 12510, 'マ');
            table.Rows.Add(144, 12511, 'ミ');
            table.Rows.Add(145, 12512, 'ム');
            table.Rows.Add(146, 12513, 'メ');
            table.Rows.Add(147, 12514, 'モ');
            table.Rows.Add(148, 12515, 'ャ');
            table.Rows.Add(149, 12516, 'ヤ');
            table.Rows.Add(150, 12517, 'ュ');
            table.Rows.Add(151, 12518, 'ユ');
            table.Rows.Add(152, 12519, 'ョ');
            table.Rows.Add(153, 12520, 'ヨ');
            table.Rows.Add(154, 12521, 'ラ');
            table.Rows.Add(155, 12522, 'リ');
            table.Rows.Add(156, 12523, 'ル');
            table.Rows.Add(157, 12524, 'レ');
            table.Rows.Add(158, 12525, 'ロ');
            table.Rows.Add(159, 12527, 'ワ');
            table.Rows.Add(160, 12530, 'ヲ');
            table.Rows.Add(161, 12531, 'ン');
            table.Rows.Add(162, 65296, '０');
            table.Rows.Add(163, 65297, '１');
            table.Rows.Add(164, 65298, '２');
            table.Rows.Add(165, 65299, '３');
            table.Rows.Add(166, 65300, '４');
            table.Rows.Add(167, 65301, '５');
            table.Rows.Add(168, 65302, '６');
            table.Rows.Add(169, 65303, '７');
            table.Rows.Add(170, 65304, '８');
            table.Rows.Add(171, 65305, '９');
            table.Rows.Add(172, 65313, 'Ａ');
            table.Rows.Add(173, 65314, 'Ｂ');
            table.Rows.Add(174, 65315, 'Ｃ');
            table.Rows.Add(175, 65316, 'Ｄ');
            table.Rows.Add(176, 65317, 'Ｅ');
            table.Rows.Add(177, 65318, 'Ｆ');
            table.Rows.Add(178, 65319, 'Ｇ');
            table.Rows.Add(179, 65320, 'Ｈ');
            table.Rows.Add(180, 65321, 'Ｉ');
            table.Rows.Add(181, 65322, 'Ｊ');
            table.Rows.Add(182, 65323, 'Ｋ');
            table.Rows.Add(183, 65324, 'Ｌ');
            table.Rows.Add(184, 65325, 'Ｍ');
            table.Rows.Add(185, 65326, 'Ｎ');
            table.Rows.Add(186, 65327, 'Ｏ');
            table.Rows.Add(187, 65328, 'Ｐ');
            table.Rows.Add(188, 65329, 'Ｑ');
            table.Rows.Add(189, 65330, 'Ｒ');
            table.Rows.Add(190, 65331, 'Ｓ');
            table.Rows.Add(191, 65332, 'Ｔ');
            table.Rows.Add(192, 65333, 'Ｕ');
            table.Rows.Add(193, 65334, 'Ｖ');
            table.Rows.Add(194, 65335, 'Ｗ');
            table.Rows.Add(195, 65336, 'Ｘ');
            table.Rows.Add(196, 65337, 'Ｙ');
            table.Rows.Add(197, 65338, 'Ｚ');
            table.Rows.Add(198, 65345, 'ａ');
            table.Rows.Add(199, 65346, 'ｂ');
            table.Rows.Add(200, 65347, 'ｃ');
            table.Rows.Add(201, 65348, 'ｄ');
            table.Rows.Add(202, 65349, 'ｅ');
            table.Rows.Add(203, 65350, 'ｆ');
            table.Rows.Add(204, 65351, 'ｇ');
            table.Rows.Add(205, 65352, 'ｈ');
            table.Rows.Add(206, 65353, 'ｉ');
            table.Rows.Add(207, 65354, 'ｊ');
            table.Rows.Add(208, 65355, 'ｋ');
            table.Rows.Add(209, 65356, 'ｌ');
            table.Rows.Add(210, 65357, 'ｍ');
            table.Rows.Add(211, 65358, 'ｎ');
            table.Rows.Add(212, 65359, 'ｏ');
            table.Rows.Add(213, 65360, 'ｐ');
            table.Rows.Add(214, 65361, 'ｑ');
            table.Rows.Add(215, 65362, 'ｒ');
            table.Rows.Add(216, 65363, 'ｓ');
            table.Rows.Add(217, 65364, 'ｔ');
            table.Rows.Add(218, 65365, 'ｕ');
            table.Rows.Add(219, 65366, 'ｖ');
            table.Rows.Add(220, 65367, 'ｗ');
            table.Rows.Add(221, 65368, 'ｘ');
            table.Rows.Add(222, 65369, 'ｙ');
            table.Rows.Add(223, 65370, 'ｚ');
            table.Rows.Add(225, 65281, '！');
            table.Rows.Add(226, 65311, '？');
            table.Rows.Add(227, 12289, '、');
            table.Rows.Add(228, 12290, '。');
            table.Rows.Add(229, 8943, '⋯');
            table.Rows.Add(230, 12539, '・');
            table.Rows.Add(231, 65295, '／');
            table.Rows.Add(232, 12300, '「');
            table.Rows.Add(233, 12301, '」');
            table.Rows.Add(234, 12302, '『');
            table.Rows.Add(235, 12303, '』');
            table.Rows.Add(236, 65288, '（');
            table.Rows.Add(237, 65289, '）');
            table.Rows.Add(238, 9325, '♂');
            table.Rows.Add(239, 9326, '♀');
            table.Rows.Add(240, 65291, '＋');
            table.Rows.Add(241, 65293, '－');
            table.Rows.Add(242, 9319, '×');
            table.Rows.Add(243, 9320, '÷');
            table.Rows.Add(244, 65309, '＝');
            table.Rows.Add(245, 65370, 'ｚ');
            table.Rows.Add(246, 65306, '：');
            table.Rows.Add(247, 65307, '；');
            table.Rows.Add(248, 65294, '．');
            table.Rows.Add(249, 65292, '，');
            table.Rows.Add(250, 9327, '♤');
            table.Rows.Add(251, 9328, '♧');
            table.Rows.Add(252, 9329, '♥');
            table.Rows.Add(253, 9330, '♢');
            table.Rows.Add(254, 9331, '☆');
            table.Rows.Add(255, 9332, '◎');
            table.Rows.Add(256, 9333, '○');
            table.Rows.Add(257, 9334, '□');
            table.Rows.Add(258, 9335, '△');
            table.Rows.Add(259, 9336, '◇');
            table.Rows.Add(260, 65312, '＠');
            table.Rows.Add(261, 9337, '♪');
            table.Rows.Add(262, 65285, '％');
            table.Rows.Add(263, 9338, '☀');
            table.Rows.Add(264, 9339, '☁');
            table.Rows.Add(265, 9341, '☂');
            table.Rows.Add(266, 10052, '❄');
            table.Rows.Add(267, 9739, '☋');
            table.Rows.Add(268, 9812, '♔');
            table.Rows.Add(269, 9813, '♕');
            table.Rows.Add(270, 9738, '☊');
            table.Rows.Add(271, 8663, '⇗');
            table.Rows.Add(272, 8664, '⇘');
            table.Rows.Add(273, 9790, '☾');
            table.Rows.Add(274, 165, '¥');
            table.Rows.Add(275, 9800, '♈');
            table.Rows.Add(276, 9801, '♉');
            table.Rows.Add(277, 9802, '♊');
            table.Rows.Add(278, 9803, '♋');
            table.Rows.Add(279, 9804, '♌');
            table.Rows.Add(280, 9805, '♍');
            table.Rows.Add(281, 9806, '♎');
            table.Rows.Add(282, 9807, '♏');
            table.Rows.Add(283, 8592, '←');
            table.Rows.Add(284, 8593, '↑');
            table.Rows.Add(285, 8595, '↓');
            table.Rows.Add(286, 8594, '→');
            table.Rows.Add(287, 8227, '‣');
            table.Rows.Add(288, 65286, '＆');
            table.Rows.Add(289, 48, '0');
            table.Rows.Add(290, 49, '1');
            table.Rows.Add(291, 50, '2');
            table.Rows.Add(292, 51, '3');
            table.Rows.Add(293, 52, '4');
            table.Rows.Add(294, 53, '5');
            table.Rows.Add(295, 54, '6');
            table.Rows.Add(296, 55, '7');
            table.Rows.Add(297, 56, '8');
            table.Rows.Add(298, 57, '9');
            table.Rows.Add(299, 65, 'A');
            table.Rows.Add(300, 66, 'B');
            table.Rows.Add(301, 67, 'C');
            table.Rows.Add(302, 68, 'D');
            table.Rows.Add(303, 69, 'E');
            table.Rows.Add(304, 70, 'F');
            table.Rows.Add(305, 71, 'G');
            table.Rows.Add(306, 72, 'H');
            table.Rows.Add(307, 73, 'I');
            table.Rows.Add(308, 74, 'J');
            table.Rows.Add(309, 75, 'K');
            table.Rows.Add(310, 76, 'L');
            table.Rows.Add(311, 77, 'M');
            table.Rows.Add(312, 78, 'N');
            table.Rows.Add(313, 79, 'O');
            table.Rows.Add(314, 80, 'P');
            table.Rows.Add(315, 81, 'Q');
            table.Rows.Add(316, 82, 'R');
            table.Rows.Add(317, 83, 'S');
            table.Rows.Add(318, 84, 'T');
            table.Rows.Add(319, 85, 'U');
            table.Rows.Add(320, 86, 'V');
            table.Rows.Add(321, 87, 'W');
            table.Rows.Add(322, 88, 'X');
            table.Rows.Add(323, 89, 'Y');
            table.Rows.Add(324, 90, 'Z');
            table.Rows.Add(325, 97, 'a');
            table.Rows.Add(326, 98, 'b');
            table.Rows.Add(327, 99, 'c');
            table.Rows.Add(328, 100, 'd');
            table.Rows.Add(329, 101, 'e');
            table.Rows.Add(330, 102, 'f');
            table.Rows.Add(331, 103, 'g');
            table.Rows.Add(332, 104, 'h');
            table.Rows.Add(333, 105, 'i');
            table.Rows.Add(334, 106, 'j');
            table.Rows.Add(335, 107, 'k');
            table.Rows.Add(336, 108, 'l');
            table.Rows.Add(337, 109, 'm');
            table.Rows.Add(338, 110, 'n');
            table.Rows.Add(339, 111, 'o');
            table.Rows.Add(340, 112, 'p');
            table.Rows.Add(341, 113, 'q');
            table.Rows.Add(342, 114, 'r');
            table.Rows.Add(343, 115, 's');
            table.Rows.Add(344, 116, 't');
            table.Rows.Add(345, 117, 'u');
            table.Rows.Add(346, 118, 'v');
            table.Rows.Add(347, 119, 'w');
            table.Rows.Add(348, 120, 'x');
            table.Rows.Add(349, 121, 'y');
            table.Rows.Add(350, 122, 'z');
            table.Rows.Add(351, 192, 'À');
            table.Rows.Add(352, 193, 'Á');
            table.Rows.Add(353, 194, 'Â');
            table.Rows.Add(354, 195, 'Ã');
            table.Rows.Add(355, 196, 'Ä');
            table.Rows.Add(356, 197, 'Å');
            table.Rows.Add(357, 198, 'Æ');
            table.Rows.Add(358, 199, 'Ç');
            table.Rows.Add(359, 200, 'È');
            table.Rows.Add(360, 201, 'É');
            table.Rows.Add(361, 202, 'Ê');
            table.Rows.Add(362, 203, 'Ë');
            table.Rows.Add(363, 204, 'Ì');
            table.Rows.Add(364, 205, 'Í');
            table.Rows.Add(365, 206, 'Î');
            table.Rows.Add(366, 207, 'Ï');
            table.Rows.Add(367, 208, 'Ð');
            table.Rows.Add(368, 209, 'Ñ');
            table.Rows.Add(369, 210, 'Ò');
            table.Rows.Add(370, 211, 'Ó');
            table.Rows.Add(371, 212, 'Ô');
            table.Rows.Add(372, 213, 'Õ');
            table.Rows.Add(373, 214, 'Ö');
            table.Rows.Add(374, 215, '×');
            table.Rows.Add(375, 216, 'Ø');
            table.Rows.Add(376, 217, 'Ù');
            table.Rows.Add(377, 218, 'Ú');
            table.Rows.Add(378, 219, 'Û');
            table.Rows.Add(379, 220, 'Ü');
            table.Rows.Add(380, 221, 'Ý');
            table.Rows.Add(381, 222, 'Þ');
            table.Rows.Add(382, 223, 'ß');
            table.Rows.Add(383, 224, 'à');
            table.Rows.Add(384, 225, 'á');
            table.Rows.Add(385, 226, 'â');
            table.Rows.Add(386, 227, 'ã');
            table.Rows.Add(387, 228, 'ä');
            table.Rows.Add(388, 229, 'å');
            table.Rows.Add(389, 230, 'æ');
            table.Rows.Add(390, 231, 'ç');
            table.Rows.Add(391, 232, 'è');
            table.Rows.Add(392, 233, 'é');
            table.Rows.Add(393, 234, 'ê');
            table.Rows.Add(394, 235, 'ë');
            table.Rows.Add(395, 236, 'ì');
            table.Rows.Add(396, 237, 'í');
            table.Rows.Add(397, 238, 'î');
            table.Rows.Add(398, 239, 'ï');
            table.Rows.Add(399, 240, 'ð');
            table.Rows.Add(400, 241, 'ñ');
            table.Rows.Add(401, 242, 'ò');
            table.Rows.Add(402, 243, 'ó');
            table.Rows.Add(403, 244, 'ô');
            table.Rows.Add(404, 245, 'õ');
            table.Rows.Add(405, 246, 'ö');
            table.Rows.Add(406, 247, '÷');
            table.Rows.Add(407, 248, 'ø');
            table.Rows.Add(408, 249, 'ù');
            table.Rows.Add(409, 250, 'ú');
            table.Rows.Add(410, 251, 'û');
            table.Rows.Add(411, 252, 'ü');
            table.Rows.Add(412, 253, 'ý');
            table.Rows.Add(413, 254, 'þ');
            table.Rows.Add(414, 255, 'ÿ');
            table.Rows.Add(415, 338, 'Œ');
            table.Rows.Add(416, 339, 'œ');
            table.Rows.Add(417, 350, 'Ş');
            table.Rows.Add(418, 351, 'ş');
            table.Rows.Add(419, 170, 'ª');
            table.Rows.Add(420, 186, 'º');
            table.Rows.Add(421, 185, '¹');
            table.Rows.Add(422, 178, '²');
            table.Rows.Add(423, 179, '³');
            table.Rows.Add(424, 36, '$');
            table.Rows.Add(425, 161, '¡');
            table.Rows.Add(426, 191, '¿');
            table.Rows.Add(427, 33, '!');
            table.Rows.Add(428, 63, '?');
            table.Rows.Add(429, 44, ',');
            table.Rows.Add(430, 46, '.');
            table.Rows.Add(431, 9324, '…');
            table.Rows.Add(432, 65381, '･');
            table.Rows.Add(433, 47, '/');
            table.Rows.Add(434, 8216, '‘');
            table.Rows.Add(435, 8217, '’');
            table.Rows.Add(436, 8220, '“');
            table.Rows.Add(437, 8221, '”');
            table.Rows.Add(438, 8222, '„');
            table.Rows.Add(439, 12298, '《');
            table.Rows.Add(440, 12299, '》');
            table.Rows.Add(441, 40, '(');
            table.Rows.Add(442, 41, ')');
            table.Rows.Add(443, 9794, '♂');
            table.Rows.Add(444, 9792, '♀');
            table.Rows.Add(445, 43, '+');
            table.Rows.Add(446, 45, '-');
            table.Rows.Add(447, 42, '*');
            table.Rows.Add(448, 35, '#');
            table.Rows.Add(449, 61, '=');
            table.Rows.Add(450, 38, '&');
            table.Rows.Add(451, 126, '~');
            table.Rows.Add(452, 58, ':');
            table.Rows.Add(453, 59, ';');
            table.Rows.Add(454, 9327, '⑯');
            table.Rows.Add(455, 9328, '⑰');
            table.Rows.Add(456, 9329, '⑱');
            table.Rows.Add(457, 9330, '⑲');
            table.Rows.Add(458, 9331, '⑳');
            table.Rows.Add(459, 9332, '⑴');
            table.Rows.Add(460, 9333, '⑵');
            table.Rows.Add(461, 9334, '⑶');
            table.Rows.Add(462, 9335, '⑷');
            table.Rows.Add(463, 9336, '⑸');
            table.Rows.Add(464, 64, '@');
            table.Rows.Add(465, 9337, '⑹');
            table.Rows.Add(466, 37, '%');
            table.Rows.Add(467, 9338, '⑺');
            table.Rows.Add(468, 9339, '⑻');
            table.Rows.Add(469, 9340, '⑼');
            table.Rows.Add(470, 9341, '⑽');
            table.Rows.Add(471, 9342, '⑾');
            table.Rows.Add(472, 9343, '⑿');
            table.Rows.Add(473, 9344, '⒀');
            table.Rows.Add(474, 9345, '⒁');
            table.Rows.Add(475, 9346, '⒂');
            table.Rows.Add(476, 9347, '⒃');
            table.Rows.Add(477, 9348, '⒄');
            table.Rows.Add(478, 32, ' ');
            table.Rows.Add(479, 9349, '⒅');
            table.Rows.Add(480, 9350, '⒆');
            table.Rows.Add(481, 9351, '⒇');
            table.Rows.Add(488, 176, '°');
            table.Rows.Add(489, 95, '_');
            table.Rows.Add(490, 65343, '＿');
            table.Rows.Add(1024, 44032, '가');
            table.Rows.Add(1025, 44033, '각');
            table.Rows.Add(1026, 44036, '간');
            table.Rows.Add(1027, 44039, '갇');
            table.Rows.Add(1028, 44040, '갈');
            table.Rows.Add(1029, 44041, '갉');
            table.Rows.Add(1030, 44042, '갊');
            table.Rows.Add(1031, 44048, '감');
            table.Rows.Add(1032, 44049, '갑');
            table.Rows.Add(1033, 44050, '값');
            table.Rows.Add(1034, 44051, '갓');
            table.Rows.Add(1035, 44052, '갔');
            table.Rows.Add(1036, 44053, '강');
            table.Rows.Add(1037, 44054, '갖');
            table.Rows.Add(1038, 44055, '갗');
            table.Rows.Add(1040, 44057, '같');
            table.Rows.Add(1041, 44058, '갚');
            table.Rows.Add(1042, 44059, '갛');
            table.Rows.Add(1043, 44060, '개');
            table.Rows.Add(1044, 44061, '객');
            table.Rows.Add(1045, 44064, '갠');
            table.Rows.Add(1046, 44068, '갤');
            table.Rows.Add(1047, 44076, '갬');
            table.Rows.Add(1048, 44077, '갭');
            table.Rows.Add(1049, 44079, '갯');
            table.Rows.Add(1050, 44080, '갰');
            table.Rows.Add(1051, 44081, '갱');
            table.Rows.Add(1052, 44088, '갸');
            table.Rows.Add(1053, 44089, '갹');
            table.Rows.Add(1054, 44092, '갼');
            table.Rows.Add(1055, 44096, '걀');
            table.Rows.Add(1056, 44107, '걋');
            table.Rows.Add(1057, 44109, '걍');
            table.Rows.Add(1058, 44116, '걔');
            table.Rows.Add(1059, 44120, '걘');
            table.Rows.Add(1060, 44124, '걜');
            table.Rows.Add(1061, 44144, '거');
            table.Rows.Add(1062, 44145, '걱');
            table.Rows.Add(1063, 44148, '건');
            table.Rows.Add(1064, 44151, '걷');
            table.Rows.Add(1065, 44152, '걸');
            table.Rows.Add(1066, 44154, '걺');
            table.Rows.Add(1067, 44160, '검');
            table.Rows.Add(1068, 44161, '겁');
            table.Rows.Add(1069, 44163, '것');
            table.Rows.Add(1070, 44164, '겄');
            table.Rows.Add(1071, 44165, '겅');
            table.Rows.Add(1072, 44166, '겆');
            table.Rows.Add(1073, 44169, '겉');
            table.Rows.Add(1074, 44170, '겊');
            table.Rows.Add(1075, 44171, '겋');
            table.Rows.Add(1076, 44172, '게');
            table.Rows.Add(1077, 44176, '겐');
            table.Rows.Add(1078, 44180, '겔');
            table.Rows.Add(1079, 44188, '겜');
            table.Rows.Add(1080, 44189, '겝');
            table.Rows.Add(1081, 44191, '겟');
            table.Rows.Add(1082, 44192, '겠');
            table.Rows.Add(1083, 44193, '겡');
            table.Rows.Add(1084, 44200, '겨');
            table.Rows.Add(1085, 44201, '격');
            table.Rows.Add(1086, 44202, '겪');
            table.Rows.Add(1087, 44204, '견');
            table.Rows.Add(1088, 44207, '겯');
            table.Rows.Add(1089, 44208, '결');
            table.Rows.Add(1090, 44216, '겸');
            table.Rows.Add(1091, 44217, '겹');
            table.Rows.Add(1092, 44219, '겻');
            table.Rows.Add(1093, 44220, '겼');
            table.Rows.Add(1094, 44221, '경');
            table.Rows.Add(1095, 44225, '곁');
            table.Rows.Add(1096, 44228, '계');
            table.Rows.Add(1097, 44232, '곈');
            table.Rows.Add(1098, 44236, '곌');
            table.Rows.Add(1099, 44245, '곕');
            table.Rows.Add(1100, 44247, '곗');
            table.Rows.Add(1101, 44256, '고');
            table.Rows.Add(1102, 44257, '곡');
            table.Rows.Add(1103, 44260, '곤');
            table.Rows.Add(1104, 44263, '곧');
            table.Rows.Add(1105, 44264, '골');
            table.Rows.Add(1106, 44266, '곪');
            table.Rows.Add(1107, 44268, '곬');
            table.Rows.Add(1108, 44271, '곯');
            table.Rows.Add(1109, 44272, '곰');
            table.Rows.Add(1110, 44273, '곱');
            table.Rows.Add(1111, 44275, '곳');
            table.Rows.Add(1112, 44277, '공');
            table.Rows.Add(1113, 44278, '곶');
            table.Rows.Add(1114, 44284, '과');
            table.Rows.Add(1115, 44285, '곽');
            table.Rows.Add(1116, 44288, '관');
            table.Rows.Add(1117, 44292, '괄');
            table.Rows.Add(1118, 44294, '괆');
            table.Rows.Add(1119, 44300, '괌');
            table.Rows.Add(1120, 44301, '괍');
            table.Rows.Add(1121, 44303, '괏');
            table.Rows.Add(1122, 44305, '광');
            table.Rows.Add(1123, 44312, '괘');
            table.Rows.Add(1124, 44316, '괜');
            table.Rows.Add(1125, 44320, '괠');
            table.Rows.Add(1126, 44329, '괩');
            table.Rows.Add(1127, 44332, '괬');
            table.Rows.Add(1128, 44333, '괭');
            table.Rows.Add(1129, 44340, '괴');
            table.Rows.Add(1130, 44341, '괵');
            table.Rows.Add(1131, 44344, '괸');
            table.Rows.Add(1132, 44348, '괼');
            table.Rows.Add(1133, 44356, '굄');
            table.Rows.Add(1134, 44357, '굅');
            table.Rows.Add(1135, 44359, '굇');
            table.Rows.Add(1136, 44361, '굉');
            table.Rows.Add(1137, 44368, '교');
            table.Rows.Add(1138, 44372, '굔');
            table.Rows.Add(1139, 44376, '굘');
            table.Rows.Add(1140, 44385, '굡');
            table.Rows.Add(1141, 44387, '굣');
            table.Rows.Add(1142, 44396, '구');
            table.Rows.Add(1143, 44397, '국');
            table.Rows.Add(1144, 44400, '군');
            table.Rows.Add(1145, 44403, '굳');
            table.Rows.Add(1146, 44404, '굴');
            table.Rows.Add(1147, 44405, '굵');
            table.Rows.Add(1148, 44406, '굶');
            table.Rows.Add(1149, 44411, '굻');
            table.Rows.Add(1150, 44412, '굼');
            table.Rows.Add(1151, 44413, '굽');
            table.Rows.Add(1152, 44415, '굿');
            table.Rows.Add(1153, 44417, '궁');
            table.Rows.Add(1154, 44418, '궂');
            table.Rows.Add(1155, 44424, '궈');
            table.Rows.Add(1156, 44425, '궉');
            table.Rows.Add(1157, 44428, '권');
            table.Rows.Add(1158, 44432, '궐');
            table.Rows.Add(1159, 44444, '궜');
            table.Rows.Add(1160, 44445, '궝');
            table.Rows.Add(1161, 44452, '궤');
            table.Rows.Add(1162, 44471, '궷');
            table.Rows.Add(1163, 44480, '귀');
            table.Rows.Add(1164, 44481, '귁');
            table.Rows.Add(1165, 44484, '귄');
            table.Rows.Add(1166, 44488, '귈');
            table.Rows.Add(1167, 44496, '귐');
            table.Rows.Add(1168, 44497, '귑');
            table.Rows.Add(1169, 44499, '귓');
            table.Rows.Add(1170, 44508, '규');
            table.Rows.Add(1171, 44512, '균');
            table.Rows.Add(1172, 44516, '귤');
            table.Rows.Add(1173, 44536, '그');
            table.Rows.Add(1174, 44537, '극');
            table.Rows.Add(1175, 44540, '근');
            table.Rows.Add(1176, 44543, '귿');
            table.Rows.Add(1177, 44544, '글');
            table.Rows.Add(1178, 44545, '긁');
            table.Rows.Add(1179, 44552, '금');
            table.Rows.Add(1180, 44553, '급');
            table.Rows.Add(1181, 44555, '긋');
            table.Rows.Add(1182, 44557, '긍');
            table.Rows.Add(1183, 44564, '긔');
            table.Rows.Add(1184, 44592, '기');
            table.Rows.Add(1185, 44593, '긱');
            table.Rows.Add(1186, 44596, '긴');
            table.Rows.Add(1187, 44599, '긷');
            table.Rows.Add(1188, 44600, '길');
            table.Rows.Add(1189, 44602, '긺');
            table.Rows.Add(1190, 44608, '김');
            table.Rows.Add(1191, 44609, '깁');
            table.Rows.Add(1192, 44611, '깃');
            table.Rows.Add(1193, 44613, '깅');
            table.Rows.Add(1194, 44614, '깆');
            table.Rows.Add(1195, 44618, '깊');
            table.Rows.Add(1196, 44620, '까');
            table.Rows.Add(1197, 44621, '깍');
            table.Rows.Add(1198, 44622, '깎');
            table.Rows.Add(1199, 44624, '깐');
            table.Rows.Add(1200, 44628, '깔');
            table.Rows.Add(1201, 44630, '깖');
            table.Rows.Add(1202, 44636, '깜');
            table.Rows.Add(1203, 44637, '깝');
            table.Rows.Add(1204, 44639, '깟');
            table.Rows.Add(1205, 44640, '깠');
            table.Rows.Add(1206, 44641, '깡');
            table.Rows.Add(1207, 44645, '깥');
            table.Rows.Add(1208, 44648, '깨');
            table.Rows.Add(1209, 44649, '깩');
            table.Rows.Add(1210, 44652, '깬');
            table.Rows.Add(1211, 44656, '깰');
            table.Rows.Add(1212, 44664, '깸');
            table.Rows.Add(1213, 44665, '깹');
            table.Rows.Add(1214, 44667, '깻');
            table.Rows.Add(1215, 44668, '깼');
            table.Rows.Add(1216, 44669, '깽');
            table.Rows.Add(1217, 44676, '꺄');
            table.Rows.Add(1218, 44677, '꺅');
            table.Rows.Add(1219, 44684, '꺌');
            table.Rows.Add(1220, 44732, '꺼');
            table.Rows.Add(1221, 44733, '꺽');
            table.Rows.Add(1222, 44734, '꺾');
            table.Rows.Add(1223, 44736, '껀');
            table.Rows.Add(1224, 44740, '껄');
            table.Rows.Add(1225, 44748, '껌');
            table.Rows.Add(1226, 44749, '껍');
            table.Rows.Add(1227, 44751, '껏');
            table.Rows.Add(1228, 44752, '껐');
            table.Rows.Add(1229, 44753, '껑');
            table.Rows.Add(1230, 44760, '께');
            table.Rows.Add(1231, 44761, '껙');
            table.Rows.Add(1232, 44764, '껜');
            table.Rows.Add(1233, 44776, '껨');
            table.Rows.Add(1234, 44779, '껫');
            table.Rows.Add(1235, 44781, '껭');
            table.Rows.Add(1236, 44788, '껴');
            table.Rows.Add(1237, 44792, '껸');
            table.Rows.Add(1238, 44796, '껼');
            table.Rows.Add(1239, 44807, '꼇');
            table.Rows.Add(1240, 44808, '꼈');
            table.Rows.Add(1241, 44813, '꼍');
            table.Rows.Add(1242, 44816, '꼐');
            table.Rows.Add(1243, 44844, '꼬');
            table.Rows.Add(1244, 44845, '꼭');
            table.Rows.Add(1245, 44848, '꼰');
            table.Rows.Add(1246, 44850, '꼲');
            table.Rows.Add(1247, 44852, '꼴');
            table.Rows.Add(1248, 44860, '꼼');
            table.Rows.Add(1249, 44861, '꼽');
            table.Rows.Add(1250, 44863, '꼿');
            table.Rows.Add(1251, 44865, '꽁');
            table.Rows.Add(1252, 44866, '꽂');
            table.Rows.Add(1253, 44867, '꽃');
            table.Rows.Add(1254, 44872, '꽈');
            table.Rows.Add(1255, 44873, '꽉');
            table.Rows.Add(1256, 44880, '꽐');
            table.Rows.Add(1257, 44892, '꽜');
            table.Rows.Add(1258, 44893, '꽝');
            table.Rows.Add(1259, 44900, '꽤');
            table.Rows.Add(1260, 44901, '꽥');
            table.Rows.Add(1261, 44921, '꽹');
            table.Rows.Add(1262, 44928, '꾀');
            table.Rows.Add(1263, 44932, '꾄');
            table.Rows.Add(1264, 44936, '꾈');
            table.Rows.Add(1265, 44944, '꾐');
            table.Rows.Add(1266, 44945, '꾑');
            table.Rows.Add(1267, 44949, '꾕');
            table.Rows.Add(1268, 44956, '꾜');
            table.Rows.Add(1269, 44984, '꾸');
            table.Rows.Add(1270, 44985, '꾹');
            table.Rows.Add(1271, 44988, '꾼');
            table.Rows.Add(1272, 44992, '꿀');
            table.Rows.Add(1273, 44999, '꿇');
            table.Rows.Add(1274, 45000, '꿈');
            table.Rows.Add(1275, 45001, '꿉');
            table.Rows.Add(1276, 45003, '꿋');
            table.Rows.Add(1277, 45005, '꿍');
            table.Rows.Add(1278, 45006, '꿎');
            table.Rows.Add(1279, 45012, '꿔');
            table.Rows.Add(1280, 45020, '꿜');
            table.Rows.Add(1281, 45032, '꿨');
            table.Rows.Add(1282, 45033, '꿩');
            table.Rows.Add(1283, 45040, '꿰');
            table.Rows.Add(1284, 45041, '꿱');
            table.Rows.Add(1285, 45044, '꿴');
            table.Rows.Add(1286, 45048, '꿸');
            table.Rows.Add(1287, 45056, '뀀');
            table.Rows.Add(1288, 45057, '뀁');
            table.Rows.Add(1289, 45060, '뀄');
            table.Rows.Add(1290, 45068, '뀌');
            table.Rows.Add(1291, 45072, '뀐');
            table.Rows.Add(1292, 45076, '뀔');
            table.Rows.Add(1293, 45084, '뀜');
            table.Rows.Add(1294, 45085, '뀝');
            table.Rows.Add(1295, 45096, '뀨');
            table.Rows.Add(1296, 45124, '끄');
            table.Rows.Add(1297, 45125, '끅');
            table.Rows.Add(1298, 45128, '끈');
            table.Rows.Add(1299, 45130, '끊');
            table.Rows.Add(1300, 45132, '끌');
            table.Rows.Add(1301, 45134, '끎');
            table.Rows.Add(1302, 45139, '끓');
            table.Rows.Add(1303, 45140, '끔');
            table.Rows.Add(1304, 45141, '끕');
            table.Rows.Add(1305, 45143, '끗');
            table.Rows.Add(1306, 45145, '끙');
            table.Rows.Add(1307, 45149, '끝');
            table.Rows.Add(1308, 45180, '끼');
            table.Rows.Add(1309, 45181, '끽');
            table.Rows.Add(1310, 45184, '낀');
            table.Rows.Add(1311, 45188, '낄');
            table.Rows.Add(1312, 45196, '낌');
            table.Rows.Add(1313, 45197, '낍');
            table.Rows.Add(1314, 45199, '낏');
            table.Rows.Add(1315, 45201, '낑');
            table.Rows.Add(1316, 45208, '나');
            table.Rows.Add(1317, 45209, '낙');
            table.Rows.Add(1318, 45210, '낚');
            table.Rows.Add(1319, 45212, '난');
            table.Rows.Add(1320, 45215, '낟');
            table.Rows.Add(1321, 45216, '날');
            table.Rows.Add(1322, 45217, '낡');
            table.Rows.Add(1323, 45218, '낢');
            table.Rows.Add(1324, 45224, '남');
            table.Rows.Add(1325, 45225, '납');
            table.Rows.Add(1326, 45227, '낫');
            table.Rows.Add(1327, 45228, '났');
            table.Rows.Add(1328, 45229, '낭');
            table.Rows.Add(1329, 45230, '낮');
            table.Rows.Add(1330, 45231, '낯');
            table.Rows.Add(1331, 45233, '낱');
            table.Rows.Add(1332, 45235, '낳');
            table.Rows.Add(1333, 45236, '내');
            table.Rows.Add(1334, 45237, '낵');
            table.Rows.Add(1335, 45240, '낸');
            table.Rows.Add(1336, 45244, '낼');
            table.Rows.Add(1337, 45252, '냄');
            table.Rows.Add(1338, 45253, '냅');
            table.Rows.Add(1339, 45255, '냇');
            table.Rows.Add(1340, 45256, '냈');
            table.Rows.Add(1341, 45257, '냉');
            table.Rows.Add(1342, 45264, '냐');
            table.Rows.Add(1343, 45265, '냑');
            table.Rows.Add(1344, 45268, '냔');
            table.Rows.Add(1345, 45272, '냘');
            table.Rows.Add(1346, 45280, '냠');
            table.Rows.Add(1347, 45285, '냥');
            table.Rows.Add(1348, 45320, '너');
            table.Rows.Add(1349, 45321, '넉');
            table.Rows.Add(1350, 45323, '넋');
            table.Rows.Add(1351, 45324, '넌');
            table.Rows.Add(1352, 45328, '널');
            table.Rows.Add(1353, 45330, '넒');
            table.Rows.Add(1354, 45331, '넓');
            table.Rows.Add(1355, 45336, '넘');
            table.Rows.Add(1356, 45337, '넙');
            table.Rows.Add(1357, 45339, '넛');
            table.Rows.Add(1358, 45340, '넜');
            table.Rows.Add(1359, 45341, '넝');
            table.Rows.Add(1360, 45347, '넣');
            table.Rows.Add(1361, 45348, '네');
            table.Rows.Add(1362, 45349, '넥');
            table.Rows.Add(1363, 45352, '넨');
            table.Rows.Add(1364, 45356, '넬');
            table.Rows.Add(1365, 45364, '넴');
            table.Rows.Add(1366, 45365, '넵');
            table.Rows.Add(1367, 45367, '넷');
            table.Rows.Add(1368, 45368, '넸');
            table.Rows.Add(1369, 45369, '넹');
            table.Rows.Add(1370, 45376, '녀');
            table.Rows.Add(1371, 45377, '녁');
            table.Rows.Add(1372, 45380, '년');
            table.Rows.Add(1373, 45384, '녈');
            table.Rows.Add(1374, 45392, '념');
            table.Rows.Add(1375, 45393, '녑');
            table.Rows.Add(1376, 45396, '녔');
            table.Rows.Add(1377, 45397, '녕');
            table.Rows.Add(1378, 45400, '녘');
            table.Rows.Add(1379, 45404, '녜');
            table.Rows.Add(1380, 45408, '녠');
            table.Rows.Add(1381, 45432, '노');
            table.Rows.Add(1382, 45433, '녹');
            table.Rows.Add(1383, 45436, '논');
            table.Rows.Add(1384, 45440, '놀');
            table.Rows.Add(1385, 45442, '놂');
            table.Rows.Add(1386, 45448, '놈');
            table.Rows.Add(1387, 45449, '놉');
            table.Rows.Add(1388, 45451, '놋');
            table.Rows.Add(1389, 45453, '농');
            table.Rows.Add(1390, 45458, '높');
            table.Rows.Add(1391, 45459, '놓');
            table.Rows.Add(1392, 45460, '놔');
            table.Rows.Add(1393, 45464, '놘');
            table.Rows.Add(1394, 45468, '놜');
            table.Rows.Add(1395, 45480, '놨');
            table.Rows.Add(1396, 45516, '뇌');
            table.Rows.Add(1397, 45520, '뇐');
            table.Rows.Add(1398, 45524, '뇔');
            table.Rows.Add(1399, 45532, '뇜');
            table.Rows.Add(1400, 45533, '뇝');
            table.Rows.Add(1401, 45535, '뇟');
            table.Rows.Add(1402, 45544, '뇨');
            table.Rows.Add(1403, 45545, '뇩');
            table.Rows.Add(1404, 45548, '뇬');
            table.Rows.Add(1405, 45552, '뇰');
            table.Rows.Add(1406, 45561, '뇹');
            table.Rows.Add(1407, 45563, '뇻');
            table.Rows.Add(1408, 45565, '뇽');
            table.Rows.Add(1409, 45572, '누');
            table.Rows.Add(1410, 45573, '눅');
            table.Rows.Add(1411, 45576, '눈');
            table.Rows.Add(1412, 45579, '눋');
            table.Rows.Add(1413, 45580, '눌');
            table.Rows.Add(1414, 45588, '눔');
            table.Rows.Add(1415, 45589, '눕');
            table.Rows.Add(1416, 45591, '눗');
            table.Rows.Add(1417, 45593, '눙');
            table.Rows.Add(1418, 45600, '눠');
            table.Rows.Add(1419, 45620, '눴');
            table.Rows.Add(1420, 45628, '눼');
            table.Rows.Add(1421, 45656, '뉘');
            table.Rows.Add(1422, 45660, '뉜');
            table.Rows.Add(1423, 45664, '뉠');
            table.Rows.Add(1424, 45672, '뉨');
            table.Rows.Add(1425, 45673, '뉩');
            table.Rows.Add(1426, 45684, '뉴');
            table.Rows.Add(1427, 45685, '뉵');
            table.Rows.Add(1428, 45692, '뉼');
            table.Rows.Add(1429, 45700, '늄');
            table.Rows.Add(1430, 45701, '늅');
            table.Rows.Add(1431, 45705, '늉');
            table.Rows.Add(1432, 45712, '느');
            table.Rows.Add(1433, 45713, '늑');
            table.Rows.Add(1434, 45716, '는');
            table.Rows.Add(1435, 45720, '늘');
            table.Rows.Add(1436, 45721, '늙');
            table.Rows.Add(1437, 45722, '늚');
            table.Rows.Add(1438, 45728, '늠');
            table.Rows.Add(1439, 45729, '늡');
            table.Rows.Add(1440, 45731, '늣');
            table.Rows.Add(1441, 45733, '능');
            table.Rows.Add(1442, 45734, '늦');
            table.Rows.Add(1443, 45738, '늪');
            table.Rows.Add(1444, 45740, '늬');
            table.Rows.Add(1445, 45744, '늰');
            table.Rows.Add(1446, 45748, '늴');
            table.Rows.Add(1447, 45768, '니');
            table.Rows.Add(1448, 45769, '닉');
            table.Rows.Add(1449, 45772, '닌');
            table.Rows.Add(1450, 45776, '닐');
            table.Rows.Add(1451, 45778, '닒');
            table.Rows.Add(1452, 45784, '님');
            table.Rows.Add(1453, 45785, '닙');
            table.Rows.Add(1454, 45787, '닛');
            table.Rows.Add(1455, 45789, '닝');
            table.Rows.Add(1456, 45794, '닢');
            table.Rows.Add(1457, 45796, '다');
            table.Rows.Add(1458, 45797, '닥');
            table.Rows.Add(1459, 45798, '닦');
            table.Rows.Add(1460, 45800, '단');
            table.Rows.Add(1461, 45803, '닫');
            table.Rows.Add(1462, 45804, '달');
            table.Rows.Add(1463, 45805, '닭');
            table.Rows.Add(1464, 45806, '닮');
            table.Rows.Add(1465, 45807, '닯');
            table.Rows.Add(1466, 45811, '닳');
            table.Rows.Add(1467, 45812, '담');
            table.Rows.Add(1468, 45813, '답');
            table.Rows.Add(1469, 45815, '닷');
            table.Rows.Add(1470, 45816, '닸');
            table.Rows.Add(1471, 45817, '당');
            table.Rows.Add(1472, 45818, '닺');
            table.Rows.Add(1473, 45819, '닻');
            table.Rows.Add(1474, 45823, '닿');
            table.Rows.Add(1475, 45824, '대');
            table.Rows.Add(1476, 45825, '댁');
            table.Rows.Add(1477, 45828, '댄');
            table.Rows.Add(1478, 45832, '댈');
            table.Rows.Add(1479, 45840, '댐');
            table.Rows.Add(1480, 45841, '댑');
            table.Rows.Add(1481, 45843, '댓');
            table.Rows.Add(1482, 45844, '댔');
            table.Rows.Add(1483, 45845, '댕');
            table.Rows.Add(1484, 45852, '댜');
            table.Rows.Add(1485, 45908, '더');
            table.Rows.Add(1486, 45909, '덕');
            table.Rows.Add(1487, 45910, '덖');
            table.Rows.Add(1488, 45912, '던');
            table.Rows.Add(1489, 45915, '덛');
            table.Rows.Add(1490, 45916, '덜');
            table.Rows.Add(1491, 45918, '덞');
            table.Rows.Add(1492, 45919, '덟');
            table.Rows.Add(1493, 45924, '덤');
            table.Rows.Add(1494, 45925, '덥');
            table.Rows.Add(1495, 45927, '덧');
            table.Rows.Add(1496, 45929, '덩');
            table.Rows.Add(1497, 45931, '덫');
            table.Rows.Add(1498, 45934, '덮');
            table.Rows.Add(1499, 45936, '데');
            table.Rows.Add(1500, 45937, '덱');
            table.Rows.Add(1501, 45940, '덴');
            table.Rows.Add(1502, 45944, '델');
            table.Rows.Add(1503, 45952, '뎀');
            table.Rows.Add(1504, 45953, '뎁');
            table.Rows.Add(1505, 45955, '뎃');
            table.Rows.Add(1506, 45956, '뎄');
            table.Rows.Add(1507, 45957, '뎅');
            table.Rows.Add(1508, 45964, '뎌');
            table.Rows.Add(1509, 45968, '뎐');
            table.Rows.Add(1510, 45972, '뎔');
            table.Rows.Add(1511, 45984, '뎠');
            table.Rows.Add(1512, 45985, '뎡');
            table.Rows.Add(1513, 45992, '뎨');
            table.Rows.Add(1514, 45996, '뎬');
            table.Rows.Add(1515, 46020, '도');
            table.Rows.Add(1516, 46021, '독');
            table.Rows.Add(1517, 46024, '돈');
            table.Rows.Add(1518, 46027, '돋');
            table.Rows.Add(1519, 46028, '돌');
            table.Rows.Add(1520, 46030, '돎');
            table.Rows.Add(1521, 46032, '돐');
            table.Rows.Add(1522, 46036, '돔');
            table.Rows.Add(1523, 46037, '돕');
            table.Rows.Add(1524, 46039, '돗');
            table.Rows.Add(1525, 46041, '동');
            table.Rows.Add(1526, 46043, '돛');
            table.Rows.Add(1527, 46045, '돝');
            table.Rows.Add(1528, 46048, '돠');
            table.Rows.Add(1529, 46052, '돤');
            table.Rows.Add(1530, 46056, '돨');
            table.Rows.Add(1531, 46076, '돼');
            table.Rows.Add(1532, 46096, '됐');
            table.Rows.Add(1533, 46104, '되');
            table.Rows.Add(1534, 46108, '된');
            table.Rows.Add(1535, 46112, '될');
            table.Rows.Add(1536, 46120, '됨');
            table.Rows.Add(1537, 46121, '됩');
            table.Rows.Add(1538, 46123, '됫');
            table.Rows.Add(1539, 46132, '됴');
            table.Rows.Add(1540, 46160, '두');
            table.Rows.Add(1541, 46161, '둑');
            table.Rows.Add(1542, 46164, '둔');
            table.Rows.Add(1543, 46168, '둘');
            table.Rows.Add(1544, 46176, '둠');
            table.Rows.Add(1545, 46177, '둡');
            table.Rows.Add(1546, 46179, '둣');
            table.Rows.Add(1547, 46181, '둥');
            table.Rows.Add(1548, 46188, '둬');
            table.Rows.Add(1549, 46208, '뒀');
            table.Rows.Add(1550, 46216, '뒈');
            table.Rows.Add(1551, 46237, '뒝');
            table.Rows.Add(1552, 46244, '뒤');
            table.Rows.Add(1553, 46248, '뒨');
            table.Rows.Add(1554, 46252, '뒬');
            table.Rows.Add(1555, 46261, '뒵');
            table.Rows.Add(1556, 46263, '뒷');
            table.Rows.Add(1557, 46265, '뒹');
            table.Rows.Add(1558, 46272, '듀');
            table.Rows.Add(1559, 46276, '듄');
            table.Rows.Add(1560, 46280, '듈');
            table.Rows.Add(1561, 46288, '듐');
            table.Rows.Add(1562, 46293, '듕');
            table.Rows.Add(1563, 46300, '드');
            table.Rows.Add(1564, 46301, '득');
            table.Rows.Add(1565, 46304, '든');
            table.Rows.Add(1566, 46307, '듣');
            table.Rows.Add(1567, 46308, '들');
            table.Rows.Add(1568, 46310, '듦');
            table.Rows.Add(1569, 46316, '듬');
            table.Rows.Add(1570, 46317, '듭');
            table.Rows.Add(1571, 46319, '듯');
            table.Rows.Add(1572, 46321, '등');
            table.Rows.Add(1573, 46328, '듸');
            table.Rows.Add(1574, 46356, '디');
            table.Rows.Add(1575, 46357, '딕');
            table.Rows.Add(1576, 46360, '딘');
            table.Rows.Add(1577, 46363, '딛');
            table.Rows.Add(1578, 46364, '딜');
            table.Rows.Add(1579, 46372, '딤');
            table.Rows.Add(1580, 46373, '딥');
            table.Rows.Add(1581, 46375, '딧');
            table.Rows.Add(1582, 46376, '딨');
            table.Rows.Add(1583, 46377, '딩');
            table.Rows.Add(1584, 46378, '딪');
            table.Rows.Add(1585, 46384, '따');
            table.Rows.Add(1586, 46385, '딱');
            table.Rows.Add(1587, 46388, '딴');
            table.Rows.Add(1588, 46392, '딸');
            table.Rows.Add(1589, 46400, '땀');
            table.Rows.Add(1590, 46401, '땁');
            table.Rows.Add(1591, 46403, '땃');
            table.Rows.Add(1592, 46404, '땄');
            table.Rows.Add(1593, 46405, '땅');
            table.Rows.Add(1594, 46411, '땋');
            table.Rows.Add(1595, 46412, '때');
            table.Rows.Add(1596, 46413, '땍');
            table.Rows.Add(1597, 46416, '땐');
            table.Rows.Add(1598, 46420, '땔');
            table.Rows.Add(1599, 46428, '땜');
            table.Rows.Add(1600, 46429, '땝');
            table.Rows.Add(1601, 46431, '땟');
            table.Rows.Add(1602, 46432, '땠');
            table.Rows.Add(1603, 46433, '땡');
            table.Rows.Add(1604, 46496, '떠');
            table.Rows.Add(1605, 46497, '떡');
            table.Rows.Add(1606, 46500, '떤');
            table.Rows.Add(1607, 46504, '떨');
            table.Rows.Add(1608, 46506, '떪');
            table.Rows.Add(1609, 46507, '떫');
            table.Rows.Add(1610, 46512, '떰');
            table.Rows.Add(1611, 46513, '떱');
            table.Rows.Add(1612, 46515, '떳');
            table.Rows.Add(1613, 46516, '떴');
            table.Rows.Add(1614, 46517, '떵');
            table.Rows.Add(1615, 46523, '떻');
            table.Rows.Add(1616, 46524, '떼');
            table.Rows.Add(1617, 46525, '떽');
            table.Rows.Add(1618, 46528, '뗀');
            table.Rows.Add(1619, 46532, '뗄');
            table.Rows.Add(1620, 46540, '뗌');
            table.Rows.Add(1621, 46541, '뗍');
            table.Rows.Add(1622, 46543, '뗏');
            table.Rows.Add(1623, 46544, '뗐');
            table.Rows.Add(1624, 46545, '뗑');
            table.Rows.Add(1625, 46552, '뗘');
            table.Rows.Add(1626, 46572, '뗬');
            table.Rows.Add(1627, 46608, '또');
            table.Rows.Add(1628, 46609, '똑');
            table.Rows.Add(1629, 46612, '똔');
            table.Rows.Add(1630, 46616, '똘');
            table.Rows.Add(1631, 46629, '똥');
            table.Rows.Add(1632, 46636, '똬');
            table.Rows.Add(1633, 46644, '똴');
            table.Rows.Add(1634, 46664, '뙈');
            table.Rows.Add(1635, 46692, '뙤');
            table.Rows.Add(1636, 46696, '뙨');
            table.Rows.Add(1637, 46748, '뚜');
            table.Rows.Add(1638, 46749, '뚝');
            table.Rows.Add(1639, 46752, '뚠');
            table.Rows.Add(1640, 46756, '뚤');
            table.Rows.Add(1641, 46763, '뚫');
            table.Rows.Add(1642, 46764, '뚬');
            table.Rows.Add(1643, 46769, '뚱');
            table.Rows.Add(1644, 46804, '뛔');
            table.Rows.Add(1645, 46832, '뛰');
            table.Rows.Add(1646, 46836, '뛴');
            table.Rows.Add(1647, 46840, '뛸');
            table.Rows.Add(1648, 46848, '뜀');
            table.Rows.Add(1649, 46849, '뜁');
            table.Rows.Add(1650, 46853, '뜅');
            table.Rows.Add(1651, 46888, '뜨');
            table.Rows.Add(1652, 46889, '뜩');
            table.Rows.Add(1653, 46892, '뜬');
            table.Rows.Add(1654, 46895, '뜯');
            table.Rows.Add(1655, 46896, '뜰');
            table.Rows.Add(1656, 46904, '뜸');
            table.Rows.Add(1657, 46905, '뜹');
            table.Rows.Add(1658, 46907, '뜻');
            table.Rows.Add(1659, 46916, '띄');
            table.Rows.Add(1660, 46920, '띈');
            table.Rows.Add(1661, 46924, '띌');
            table.Rows.Add(1662, 46932, '띔');
            table.Rows.Add(1663, 46933, '띕');
            table.Rows.Add(1664, 46944, '띠');
            table.Rows.Add(1665, 46948, '띤');
            table.Rows.Add(1666, 46952, '띨');
            table.Rows.Add(1667, 46960, '띰');
            table.Rows.Add(1668, 46961, '띱');
            table.Rows.Add(1669, 46963, '띳');
            table.Rows.Add(1670, 46965, '띵');
            table.Rows.Add(1671, 46972, '라');
            table.Rows.Add(1672, 46973, '락');
            table.Rows.Add(1673, 46976, '란');
            table.Rows.Add(1674, 46980, '랄');
            table.Rows.Add(1675, 46988, '람');
            table.Rows.Add(1676, 46989, '랍');
            table.Rows.Add(1677, 46991, '랏');
            table.Rows.Add(1678, 46992, '랐');
            table.Rows.Add(1679, 46993, '랑');
            table.Rows.Add(1680, 46994, '랒');
            table.Rows.Add(1681, 46998, '랖');
            table.Rows.Add(1682, 46999, '랗');
            table.Rows.Add(1683, 47000, '래');
            table.Rows.Add(1684, 47001, '랙');
            table.Rows.Add(1685, 47004, '랜');
            table.Rows.Add(1686, 47008, '랠');
            table.Rows.Add(1687, 47016, '램');
            table.Rows.Add(1688, 47017, '랩');
            table.Rows.Add(1689, 47019, '랫');
            table.Rows.Add(1690, 47020, '랬');
            table.Rows.Add(1691, 47021, '랭');
            table.Rows.Add(1692, 47028, '랴');
            table.Rows.Add(1693, 47029, '략');
            table.Rows.Add(1694, 47032, '랸');
            table.Rows.Add(1695, 47047, '럇');
            table.Rows.Add(1696, 47049, '량');
            table.Rows.Add(1697, 47084, '러');
            table.Rows.Add(1698, 47085, '럭');
            table.Rows.Add(1699, 47088, '런');
            table.Rows.Add(1700, 47092, '럴');
            table.Rows.Add(1701, 47100, '럼');
            table.Rows.Add(1702, 47101, '럽');
            table.Rows.Add(1703, 47103, '럿');
            table.Rows.Add(1704, 47104, '렀');
            table.Rows.Add(1705, 47105, '렁');
            table.Rows.Add(1706, 47111, '렇');
            table.Rows.Add(1707, 47112, '레');
            table.Rows.Add(1708, 47113, '렉');
            table.Rows.Add(1709, 47116, '렌');
            table.Rows.Add(1710, 47120, '렐');
            table.Rows.Add(1711, 47128, '렘');
            table.Rows.Add(1712, 47129, '렙');
            table.Rows.Add(1713, 47131, '렛');
            table.Rows.Add(1714, 47133, '렝');
            table.Rows.Add(1715, 47140, '려');
            table.Rows.Add(1716, 47141, '력');
            table.Rows.Add(1717, 47144, '련');
            table.Rows.Add(1718, 47148, '렬');
            table.Rows.Add(1719, 47156, '렴');
            table.Rows.Add(1720, 47157, '렵');
            table.Rows.Add(1721, 47159, '렷');
            table.Rows.Add(1722, 47160, '렸');
            table.Rows.Add(1723, 47161, '령');
            table.Rows.Add(1724, 47168, '례');
            table.Rows.Add(1725, 47172, '롄');
            table.Rows.Add(1726, 47185, '롑');
            table.Rows.Add(1727, 47187, '롓');
            table.Rows.Add(1728, 47196, '로');
            table.Rows.Add(1729, 47197, '록');
            table.Rows.Add(1730, 47200, '론');
            table.Rows.Add(1731, 47204, '롤');
            table.Rows.Add(1732, 47212, '롬');
            table.Rows.Add(1733, 47213, '롭');
            table.Rows.Add(1734, 47215, '롯');
            table.Rows.Add(1735, 47217, '롱');
            table.Rows.Add(1736, 47224, '롸');
            table.Rows.Add(1737, 47228, '롼');
            table.Rows.Add(1738, 47245, '뢍');
            table.Rows.Add(1739, 47272, '뢨');
            table.Rows.Add(1740, 47280, '뢰');
            table.Rows.Add(1741, 47284, '뢴');
            table.Rows.Add(1742, 47288, '뢸');
            table.Rows.Add(1743, 47296, '룀');
            table.Rows.Add(1744, 47297, '룁');
            table.Rows.Add(1745, 47299, '룃');
            table.Rows.Add(1746, 47301, '룅');
            table.Rows.Add(1747, 47308, '료');
            table.Rows.Add(1748, 47312, '룐');
            table.Rows.Add(1749, 47316, '룔');
            table.Rows.Add(1750, 47325, '룝');
            table.Rows.Add(1751, 47327, '룟');
            table.Rows.Add(1752, 47329, '룡');
            table.Rows.Add(1753, 47336, '루');
            table.Rows.Add(1754, 47337, '룩');
            table.Rows.Add(1755, 47340, '룬');
            table.Rows.Add(1756, 47344, '룰');
            table.Rows.Add(1757, 47352, '룸');
            table.Rows.Add(1758, 47353, '룹');
            table.Rows.Add(1759, 47355, '룻');
            table.Rows.Add(1760, 47357, '룽');
            table.Rows.Add(1761, 47364, '뤄');
            table.Rows.Add(1762, 47384, '뤘');
            table.Rows.Add(1763, 47392, '뤠');
            table.Rows.Add(1764, 47420, '뤼');
            table.Rows.Add(1765, 47421, '뤽');
            table.Rows.Add(1766, 47424, '륀');
            table.Rows.Add(1767, 47428, '륄');
            table.Rows.Add(1768, 47436, '륌');
            table.Rows.Add(1769, 47439, '륏');
            table.Rows.Add(1770, 47441, '륑');
            table.Rows.Add(1771, 47448, '류');
            table.Rows.Add(1772, 47449, '륙');
            table.Rows.Add(1773, 47452, '륜');
            table.Rows.Add(1774, 47456, '률');
            table.Rows.Add(1775, 47464, '륨');
            table.Rows.Add(1776, 47465, '륩');
            table.Rows.Add(1777, 47467, '륫');
            table.Rows.Add(1778, 47469, '륭');
            table.Rows.Add(1779, 47476, '르');
            table.Rows.Add(1780, 47477, '륵');
            table.Rows.Add(1781, 47480, '른');
            table.Rows.Add(1782, 47484, '를');
            table.Rows.Add(1783, 47492, '름');
            table.Rows.Add(1784, 47493, '릅');
            table.Rows.Add(1785, 47495, '릇');
            table.Rows.Add(1786, 47497, '릉');
            table.Rows.Add(1787, 47498, '릊');
            table.Rows.Add(1788, 47501, '릍');
            table.Rows.Add(1789, 47502, '릎');
            table.Rows.Add(1790, 47532, '리');
            table.Rows.Add(1791, 47533, '릭');
            table.Rows.Add(1792, 47536, '린');
            table.Rows.Add(1793, 47540, '릴');
            table.Rows.Add(1794, 47548, '림');
            table.Rows.Add(1795, 47549, '립');
            table.Rows.Add(1796, 47551, '릿');
            table.Rows.Add(1797, 47553, '링');
            table.Rows.Add(1798, 47560, '마');
            table.Rows.Add(1799, 47561, '막');
            table.Rows.Add(1800, 47564, '만');
            table.Rows.Add(1801, 47566, '많');
            table.Rows.Add(1802, 47567, '맏');
            table.Rows.Add(1803, 47568, '말');
            table.Rows.Add(1804, 47569, '맑');
            table.Rows.Add(1805, 47570, '맒');
            table.Rows.Add(1806, 47576, '맘');
            table.Rows.Add(1807, 47577, '맙');
            table.Rows.Add(1808, 47579, '맛');
            table.Rows.Add(1809, 47581, '망');
            table.Rows.Add(1810, 47582, '맞');
            table.Rows.Add(1811, 47585, '맡');
            table.Rows.Add(1812, 47587, '맣');
            table.Rows.Add(1813, 47588, '매');
            table.Rows.Add(1814, 47589, '맥');
            table.Rows.Add(1815, 47592, '맨');
            table.Rows.Add(1816, 47596, '맬');
            table.Rows.Add(1817, 47604, '맴');
            table.Rows.Add(1818, 47605, '맵');
            table.Rows.Add(1819, 47607, '맷');
            table.Rows.Add(1820, 47608, '맸');
            table.Rows.Add(1821, 47609, '맹');
            table.Rows.Add(1822, 47610, '맺');
            table.Rows.Add(1823, 47616, '먀');
            table.Rows.Add(1824, 47617, '먁');
            table.Rows.Add(1825, 47624, '먈');
            table.Rows.Add(1826, 47637, '먕');
            table.Rows.Add(1827, 47672, '머');
            table.Rows.Add(1828, 47673, '먹');
            table.Rows.Add(1829, 47676, '먼');
            table.Rows.Add(1830, 47680, '멀');
            table.Rows.Add(1831, 47682, '멂');
            table.Rows.Add(1832, 47688, '멈');
            table.Rows.Add(1833, 47689, '멉');
            table.Rows.Add(1834, 47691, '멋');
            table.Rows.Add(1835, 47693, '멍');
            table.Rows.Add(1836, 47694, '멎');
            table.Rows.Add(1837, 47699, '멓');
            table.Rows.Add(1838, 47700, '메');
            table.Rows.Add(1839, 47701, '멕');
            table.Rows.Add(1840, 47704, '멘');
            table.Rows.Add(1841, 47708, '멜');
            table.Rows.Add(1842, 47716, '멤');
            table.Rows.Add(1843, 47717, '멥');
            table.Rows.Add(1844, 47719, '멧');
            table.Rows.Add(1845, 47720, '멨');
            table.Rows.Add(1846, 47721, '멩');
            table.Rows.Add(1847, 47728, '며');
            table.Rows.Add(1848, 47729, '멱');
            table.Rows.Add(1849, 47732, '면');
            table.Rows.Add(1850, 47736, '멸');
            table.Rows.Add(1851, 47747, '몃');
            table.Rows.Add(1852, 47748, '몄');
            table.Rows.Add(1853, 47749, '명');
            table.Rows.Add(1854, 47751, '몇');
            table.Rows.Add(1855, 47756, '몌');
            table.Rows.Add(1856, 47784, '모');
            table.Rows.Add(1857, 47785, '목');
            table.Rows.Add(1858, 47787, '몫');
            table.Rows.Add(1859, 47788, '몬');
            table.Rows.Add(1860, 47792, '몰');
            table.Rows.Add(1861, 47794, '몲');
            table.Rows.Add(1862, 47800, '몸');
            table.Rows.Add(1863, 47801, '몹');
            table.Rows.Add(1864, 47803, '못');
            table.Rows.Add(1865, 47805, '몽');
            table.Rows.Add(1866, 47812, '뫄');
            table.Rows.Add(1867, 47816, '뫈');
            table.Rows.Add(1868, 47832, '뫘');
            table.Rows.Add(1869, 47833, '뫙');
            table.Rows.Add(1870, 47868, '뫼');
            table.Rows.Add(1871, 47872, '묀');
            table.Rows.Add(1872, 47876, '묄');
            table.Rows.Add(1873, 47885, '묍');
            table.Rows.Add(1874, 47887, '묏');
            table.Rows.Add(1875, 47889, '묑');
            table.Rows.Add(1876, 47896, '묘');
            table.Rows.Add(1877, 47900, '묜');
            table.Rows.Add(1878, 47904, '묠');
            table.Rows.Add(1879, 47913, '묩');
            table.Rows.Add(1880, 47915, '묫');
            table.Rows.Add(1881, 47924, '무');
            table.Rows.Add(1882, 47925, '묵');
            table.Rows.Add(1883, 47926, '묶');
            table.Rows.Add(1884, 47928, '문');
            table.Rows.Add(1885, 47931, '묻');
            table.Rows.Add(1886, 47932, '물');
            table.Rows.Add(1887, 47933, '묽');
            table.Rows.Add(1888, 47934, '묾');
            table.Rows.Add(1889, 47940, '뭄');
            table.Rows.Add(1890, 47941, '뭅');
            table.Rows.Add(1891, 47943, '뭇');
            table.Rows.Add(1892, 47945, '뭉');
            table.Rows.Add(1893, 47949, '뭍');
            table.Rows.Add(1894, 47951, '뭏');
            table.Rows.Add(1895, 47952, '뭐');
            table.Rows.Add(1896, 47956, '뭔');
            table.Rows.Add(1897, 47960, '뭘');
            table.Rows.Add(1898, 47969, '뭡');
            table.Rows.Add(1899, 47971, '뭣');
            table.Rows.Add(1900, 47980, '뭬');
            table.Rows.Add(1901, 48008, '뮈');
            table.Rows.Add(1902, 48012, '뮌');
            table.Rows.Add(1903, 48016, '뮐');
            table.Rows.Add(1904, 48036, '뮤');
            table.Rows.Add(1905, 48040, '뮨');
            table.Rows.Add(1906, 48044, '뮬');
            table.Rows.Add(1907, 48052, '뮴');
            table.Rows.Add(1908, 48055, '뮷');
            table.Rows.Add(1909, 48064, '므');
            table.Rows.Add(1910, 48068, '믄');
            table.Rows.Add(1911, 48072, '믈');
            table.Rows.Add(1912, 48080, '믐');
            table.Rows.Add(1913, 48083, '믓');
            table.Rows.Add(1914, 48120, '미');
            table.Rows.Add(1915, 48121, '믹');
            table.Rows.Add(1916, 48124, '민');
            table.Rows.Add(1917, 48127, '믿');
            table.Rows.Add(1918, 48128, '밀');
            table.Rows.Add(1919, 48130, '밂');
            table.Rows.Add(1920, 48136, '밈');
            table.Rows.Add(1921, 48137, '밉');
            table.Rows.Add(1922, 48139, '밋');
            table.Rows.Add(1923, 48140, '밌');
            table.Rows.Add(1924, 48141, '밍');
            table.Rows.Add(1925, 48143, '및');
            table.Rows.Add(1926, 48145, '밑');
            table.Rows.Add(1927, 48148, '바');
            table.Rows.Add(1928, 48149, '박');
            table.Rows.Add(1929, 48150, '밖');
            table.Rows.Add(1930, 48151, '밗');
            table.Rows.Add(1931, 48152, '반');
            table.Rows.Add(1932, 48155, '받');
            table.Rows.Add(1933, 48156, '발');
            table.Rows.Add(1934, 48157, '밝');
            table.Rows.Add(1935, 48158, '밞');
            table.Rows.Add(1936, 48159, '밟');
            table.Rows.Add(1937, 48164, '밤');
            table.Rows.Add(1938, 48165, '밥');
            table.Rows.Add(1939, 48167, '밧');
            table.Rows.Add(1940, 48169, '방');
            table.Rows.Add(1941, 48173, '밭');
            table.Rows.Add(1942, 48176, '배');
            table.Rows.Add(1943, 48177, '백');
            table.Rows.Add(1944, 48180, '밴');
            table.Rows.Add(1945, 48184, '밸');
            table.Rows.Add(1946, 48192, '뱀');
            table.Rows.Add(1947, 48193, '뱁');
            table.Rows.Add(1948, 48195, '뱃');
            table.Rows.Add(1949, 48196, '뱄');
            table.Rows.Add(1950, 48197, '뱅');
            table.Rows.Add(1951, 48201, '뱉');
            table.Rows.Add(1952, 48204, '뱌');
            table.Rows.Add(1953, 48205, '뱍');
            table.Rows.Add(1954, 48208, '뱐');
            table.Rows.Add(1955, 48221, '뱝');
            table.Rows.Add(1956, 48260, '버');
            table.Rows.Add(1957, 48261, '벅');
            table.Rows.Add(1958, 48264, '번');
            table.Rows.Add(1959, 48267, '벋');
            table.Rows.Add(1960, 48268, '벌');
            table.Rows.Add(1961, 48270, '벎');
            table.Rows.Add(1962, 48276, '범');
            table.Rows.Add(1963, 48277, '법');
            table.Rows.Add(1964, 48279, '벗');
            table.Rows.Add(1965, 48281, '벙');
            table.Rows.Add(1966, 48282, '벚');
            table.Rows.Add(1967, 48288, '베');
            table.Rows.Add(1968, 48289, '벡');
            table.Rows.Add(1969, 48292, '벤');
            table.Rows.Add(1970, 48295, '벧');
            table.Rows.Add(1971, 48296, '벨');
            table.Rows.Add(1972, 48304, '벰');
            table.Rows.Add(1973, 48305, '벱');
            table.Rows.Add(1974, 48307, '벳');
            table.Rows.Add(1975, 48308, '벴');
            table.Rows.Add(1976, 48309, '벵');
            table.Rows.Add(1977, 48316, '벼');
            table.Rows.Add(1978, 48317, '벽');
            table.Rows.Add(1979, 48320, '변');
            table.Rows.Add(1980, 48324, '별');
            table.Rows.Add(1981, 48333, '볍');
            table.Rows.Add(1982, 48335, '볏');
            table.Rows.Add(1983, 48336, '볐');
            table.Rows.Add(1984, 48337, '병');
            table.Rows.Add(1985, 48341, '볕');
            table.Rows.Add(1986, 48344, '볘');
            table.Rows.Add(1987, 48348, '볜');
            table.Rows.Add(1988, 48372, '보');
            table.Rows.Add(1989, 48373, '복');
            table.Rows.Add(1990, 48374, '볶');
            table.Rows.Add(1991, 48376, '본');
            table.Rows.Add(1992, 48380, '볼');
            table.Rows.Add(1993, 48388, '봄');
            table.Rows.Add(1994, 48389, '봅');
            table.Rows.Add(1995, 48391, '봇');
            table.Rows.Add(1996, 48393, '봉');
            table.Rows.Add(1997, 48400, '봐');
            table.Rows.Add(1998, 48404, '봔');
            table.Rows.Add(1999, 48420, '봤');
            table.Rows.Add(2000, 48428, '봬');
            table.Rows.Add(2001, 48448, '뵀');
            table.Rows.Add(2002, 48456, '뵈');
            table.Rows.Add(2003, 48457, '뵉');
            table.Rows.Add(2004, 48460, '뵌');
            table.Rows.Add(2005, 48464, '뵐');
            table.Rows.Add(2006, 48472, '뵘');
            table.Rows.Add(2007, 48473, '뵙');
            table.Rows.Add(2008, 48484, '뵤');
            table.Rows.Add(2009, 48488, '뵨');
            table.Rows.Add(2010, 48512, '부');
            table.Rows.Add(2011, 48513, '북');
            table.Rows.Add(2012, 48516, '분');
            table.Rows.Add(2013, 48519, '붇');
            table.Rows.Add(2014, 48520, '불');
            table.Rows.Add(2015, 48521, '붉');
            table.Rows.Add(2016, 48522, '붊');
            table.Rows.Add(2017, 48528, '붐');
            table.Rows.Add(2018, 48529, '붑');
            table.Rows.Add(2019, 48531, '붓');
            table.Rows.Add(2020, 48533, '붕');
            table.Rows.Add(2021, 48537, '붙');
            table.Rows.Add(2022, 48538, '붚');
            table.Rows.Add(2023, 48540, '붜');
            table.Rows.Add(2024, 48548, '붤');
            table.Rows.Add(2025, 48560, '붰');
            table.Rows.Add(2026, 48568, '붸');
            table.Rows.Add(2027, 48596, '뷔');
            table.Rows.Add(2028, 48597, '뷕');
            table.Rows.Add(2029, 48600, '뷘');
            table.Rows.Add(2030, 48604, '뷜');
            table.Rows.Add(2031, 48617, '뷩');
            table.Rows.Add(2032, 48624, '뷰');
            table.Rows.Add(2033, 48628, '뷴');
            table.Rows.Add(2034, 48632, '뷸');
            table.Rows.Add(2035, 48640, '븀');
            table.Rows.Add(2036, 48643, '븃');
            table.Rows.Add(2037, 48645, '븅');
            table.Rows.Add(2038, 48652, '브');
            table.Rows.Add(2039, 48653, '븍');
            table.Rows.Add(2040, 48656, '븐');
            table.Rows.Add(2041, 48660, '블');
            table.Rows.Add(2042, 48668, '븜');
            table.Rows.Add(2043, 48669, '븝');
            table.Rows.Add(2044, 48671, '븟');
            table.Rows.Add(2045, 48708, '비');
            table.Rows.Add(2046, 48709, '빅');
            table.Rows.Add(2047, 48712, '빈');
            table.Rows.Add(2048, 48716, '빌');
            table.Rows.Add(2049, 48718, '빎');
            table.Rows.Add(2050, 48724, '빔');
            table.Rows.Add(2051, 48725, '빕');
            table.Rows.Add(2052, 48727, '빗');
            table.Rows.Add(2053, 48729, '빙');
            table.Rows.Add(2054, 48730, '빚');
            table.Rows.Add(2055, 48731, '빛');
            table.Rows.Add(2056, 48736, '빠');
            table.Rows.Add(2057, 48737, '빡');
            table.Rows.Add(2058, 48740, '빤');
            table.Rows.Add(2059, 48744, '빨');
            table.Rows.Add(2060, 48746, '빪');
            table.Rows.Add(2061, 48752, '빰');
            table.Rows.Add(2062, 48753, '빱');
            table.Rows.Add(2063, 48755, '빳');
            table.Rows.Add(2064, 48756, '빴');
            table.Rows.Add(2065, 48757, '빵');
            table.Rows.Add(2066, 48763, '빻');
            table.Rows.Add(2067, 48764, '빼');
            table.Rows.Add(2068, 48765, '빽');
            table.Rows.Add(2069, 48768, '뺀');
            table.Rows.Add(2070, 48772, '뺄');
            table.Rows.Add(2071, 48780, '뺌');
            table.Rows.Add(2072, 48781, '뺍');
            table.Rows.Add(2073, 48783, '뺏');
            table.Rows.Add(2074, 48784, '뺐');
            table.Rows.Add(2075, 48785, '뺑');
            table.Rows.Add(2076, 48792, '뺘');
            table.Rows.Add(2077, 48793, '뺙');
            table.Rows.Add(2078, 48808, '뺨');
            table.Rows.Add(2079, 48848, '뻐');
            table.Rows.Add(2080, 48849, '뻑');
            table.Rows.Add(2081, 48852, '뻔');
            table.Rows.Add(2082, 48855, '뻗');
            table.Rows.Add(2083, 48856, '뻘');
            table.Rows.Add(2084, 48864, '뻠');
            table.Rows.Add(2085, 48867, '뻣');
            table.Rows.Add(2086, 48868, '뻤');
            table.Rows.Add(2087, 48869, '뻥');
            table.Rows.Add(2088, 48876, '뻬');
            table.Rows.Add(2089, 48897, '뼁');
            table.Rows.Add(2090, 48904, '뼈');
            table.Rows.Add(2091, 48905, '뼉');
            table.Rows.Add(2092, 48920, '뼘');
            table.Rows.Add(2093, 48921, '뼙');
            table.Rows.Add(2094, 48923, '뼛');
            table.Rows.Add(2095, 48924, '뼜');
            table.Rows.Add(2096, 48925, '뼝');
            table.Rows.Add(2097, 48960, '뽀');
            table.Rows.Add(2098, 48961, '뽁');
            table.Rows.Add(2099, 48964, '뽄');
            table.Rows.Add(2100, 48968, '뽈');
            table.Rows.Add(2101, 48976, '뽐');
            table.Rows.Add(2102, 48977, '뽑');
            table.Rows.Add(2103, 48981, '뽕');
            table.Rows.Add(2104, 49044, '뾔');
            table.Rows.Add(2105, 49072, '뾰');
            table.Rows.Add(2106, 49093, '뿅');
            table.Rows.Add(2107, 49100, '뿌');
            table.Rows.Add(2108, 49101, '뿍');
            table.Rows.Add(2109, 49104, '뿐');
            table.Rows.Add(2110, 49108, '뿔');
            table.Rows.Add(2111, 49116, '뿜');
            table.Rows.Add(2112, 49119, '뿟');
            table.Rows.Add(2113, 49121, '뿡');
            table.Rows.Add(2114, 49212, '쀼');
            table.Rows.Add(2115, 49233, '쁑');
            table.Rows.Add(2116, 49240, '쁘');
            table.Rows.Add(2117, 49244, '쁜');
            table.Rows.Add(2118, 49248, '쁠');
            table.Rows.Add(2119, 49256, '쁨');
            table.Rows.Add(2120, 49257, '쁩');
            table.Rows.Add(2121, 49296, '삐');
            table.Rows.Add(2122, 49297, '삑');
            table.Rows.Add(2123, 49300, '삔');
            table.Rows.Add(2124, 49304, '삘');
            table.Rows.Add(2125, 49312, '삠');
            table.Rows.Add(2126, 49313, '삡');
            table.Rows.Add(2127, 49315, '삣');
            table.Rows.Add(2128, 49317, '삥');
            table.Rows.Add(2129, 49324, '사');
            table.Rows.Add(2130, 49325, '삭');
            table.Rows.Add(2131, 49327, '삯');
            table.Rows.Add(2132, 49328, '산');
            table.Rows.Add(2133, 49331, '삳');
            table.Rows.Add(2134, 49332, '살');
            table.Rows.Add(2135, 49333, '삵');
            table.Rows.Add(2136, 49334, '삶');
            table.Rows.Add(2137, 49340, '삼');
            table.Rows.Add(2138, 49341, '삽');
            table.Rows.Add(2139, 49343, '삿');
            table.Rows.Add(2140, 49344, '샀');
            table.Rows.Add(2141, 49345, '상');
            table.Rows.Add(2142, 49349, '샅');
            table.Rows.Add(2143, 49352, '새');
            table.Rows.Add(2144, 49353, '색');
            table.Rows.Add(2145, 49356, '샌');
            table.Rows.Add(2146, 49360, '샐');
            table.Rows.Add(2147, 49368, '샘');
            table.Rows.Add(2148, 49369, '샙');
            table.Rows.Add(2149, 49371, '샛');
            table.Rows.Add(2150, 49372, '샜');
            table.Rows.Add(2151, 49373, '생');
            table.Rows.Add(2152, 49380, '샤');
            table.Rows.Add(2153, 49381, '샥');
            table.Rows.Add(2154, 49384, '샨');
            table.Rows.Add(2155, 49388, '샬');
            table.Rows.Add(2156, 49396, '샴');
            table.Rows.Add(2157, 49397, '샵');
            table.Rows.Add(2158, 49399, '샷');
            table.Rows.Add(2159, 49401, '샹');
            table.Rows.Add(2160, 49408, '섀');
            table.Rows.Add(2161, 49412, '섄');
            table.Rows.Add(2162, 49416, '섈');
            table.Rows.Add(2163, 49424, '섐');
            table.Rows.Add(2164, 49429, '섕');
            table.Rows.Add(2165, 49436, '서');
            table.Rows.Add(2166, 49437, '석');
            table.Rows.Add(2167, 49438, '섞');
            table.Rows.Add(2168, 49439, '섟');
            table.Rows.Add(2169, 49440, '선');
            table.Rows.Add(2170, 49443, '섣');
            table.Rows.Add(2171, 49444, '설');
            table.Rows.Add(2172, 49446, '섦');
            table.Rows.Add(2173, 49447, '섧');
            table.Rows.Add(2174, 49452, '섬');
            table.Rows.Add(2175, 49453, '섭');
            table.Rows.Add(2176, 49455, '섯');
            table.Rows.Add(2177, 49456, '섰');
            table.Rows.Add(2178, 49457, '성');
            table.Rows.Add(2179, 49462, '섶');
            table.Rows.Add(2180, 49464, '세');
            table.Rows.Add(2181, 49465, '섹');
            table.Rows.Add(2182, 49468, '센');
            table.Rows.Add(2183, 49472, '셀');
            table.Rows.Add(2184, 49480, '셈');
            table.Rows.Add(2185, 49481, '셉');
            table.Rows.Add(2186, 49483, '셋');
            table.Rows.Add(2187, 49484, '셌');
            table.Rows.Add(2188, 49485, '셍');
            table.Rows.Add(2189, 49492, '셔');
            table.Rows.Add(2190, 49493, '셕');
            table.Rows.Add(2191, 49496, '션');
            table.Rows.Add(2192, 49500, '셜');
            table.Rows.Add(2193, 49508, '셤');
            table.Rows.Add(2194, 49509, '셥');
            table.Rows.Add(2195, 49511, '셧');
            table.Rows.Add(2196, 49512, '셨');
            table.Rows.Add(2197, 49513, '셩');
            table.Rows.Add(2198, 49520, '셰');
            table.Rows.Add(2199, 49524, '셴');
            table.Rows.Add(2200, 49528, '셸');
            table.Rows.Add(2201, 49541, '솅');
            table.Rows.Add(2202, 49548, '소');
            table.Rows.Add(2203, 49549, '속');
            table.Rows.Add(2204, 49550, '솎');
            table.Rows.Add(2205, 49552, '손');
            table.Rows.Add(2206, 49556, '솔');
            table.Rows.Add(2207, 49558, '솖');
            table.Rows.Add(2208, 49564, '솜');
            table.Rows.Add(2209, 49565, '솝');
            table.Rows.Add(2210, 49567, '솟');
            table.Rows.Add(2211, 49569, '송');
            table.Rows.Add(2212, 49573, '솥');
            table.Rows.Add(2213, 49576, '솨');
            table.Rows.Add(2214, 49577, '솩');
            table.Rows.Add(2215, 49580, '솬');
            table.Rows.Add(2216, 49584, '솰');
            table.Rows.Add(2217, 49597, '솽');
            table.Rows.Add(2218, 49604, '쇄');
            table.Rows.Add(2219, 49608, '쇈');
            table.Rows.Add(2220, 49612, '쇌');
            table.Rows.Add(2221, 49620, '쇔');
            table.Rows.Add(2222, 49623, '쇗');
            table.Rows.Add(2223, 49624, '쇘');
            table.Rows.Add(2224, 49632, '쇠');
            table.Rows.Add(2225, 49636, '쇤');
            table.Rows.Add(2226, 49640, '쇨');
            table.Rows.Add(2227, 49648, '쇰');
            table.Rows.Add(2228, 49649, '쇱');
            table.Rows.Add(2229, 49651, '쇳');
            table.Rows.Add(2230, 49660, '쇼');
            table.Rows.Add(2231, 49661, '쇽');
            table.Rows.Add(2232, 49664, '숀');
            table.Rows.Add(2233, 49668, '숄');
            table.Rows.Add(2234, 49676, '숌');
            table.Rows.Add(2235, 49677, '숍');
            table.Rows.Add(2236, 49679, '숏');
            table.Rows.Add(2237, 49681, '숑');
            table.Rows.Add(2238, 49688, '수');
            table.Rows.Add(2239, 49689, '숙');
            table.Rows.Add(2240, 49692, '순');
            table.Rows.Add(2241, 49695, '숟');
            table.Rows.Add(2242, 49696, '술');
            table.Rows.Add(2243, 49704, '숨');
            table.Rows.Add(2244, 49705, '숩');
            table.Rows.Add(2245, 49707, '숫');
            table.Rows.Add(2246, 49709, '숭');
            table.Rows.Add(2247, 49711, '숯');
            table.Rows.Add(2248, 49713, '숱');
            table.Rows.Add(2249, 49714, '숲');
            table.Rows.Add(2250, 49716, '숴');
            table.Rows.Add(2251, 49736, '쉈');
            table.Rows.Add(2252, 49744, '쉐');
            table.Rows.Add(2253, 49745, '쉑');
            table.Rows.Add(2254, 49748, '쉔');
            table.Rows.Add(2255, 49752, '쉘');
            table.Rows.Add(2256, 49760, '쉠');
            table.Rows.Add(2257, 49765, '쉥');
            table.Rows.Add(2258, 49772, '쉬');
            table.Rows.Add(2259, 49773, '쉭');
            table.Rows.Add(2260, 49776, '쉰');
            table.Rows.Add(2261, 49780, '쉴');
            table.Rows.Add(2262, 49788, '쉼');
            table.Rows.Add(2263, 49789, '쉽');
            table.Rows.Add(2264, 49791, '쉿');
            table.Rows.Add(2265, 49793, '슁');
            table.Rows.Add(2266, 49800, '슈');
            table.Rows.Add(2267, 49801, '슉');
            table.Rows.Add(2268, 49808, '슐');
            table.Rows.Add(2269, 49816, '슘');
            table.Rows.Add(2270, 49819, '슛');
            table.Rows.Add(2271, 49821, '슝');
            table.Rows.Add(2272, 49828, '스');
            table.Rows.Add(2273, 49829, '슥');
            table.Rows.Add(2274, 49832, '슨');
            table.Rows.Add(2275, 49836, '슬');
            table.Rows.Add(2276, 49837, '슭');
            table.Rows.Add(2277, 49844, '슴');
            table.Rows.Add(2278, 49845, '습');
            table.Rows.Add(2279, 49847, '슷');
            table.Rows.Add(2280, 49849, '승');
            table.Rows.Add(2281, 49884, '시');
            table.Rows.Add(2282, 49885, '식');
            table.Rows.Add(2283, 49888, '신');
            table.Rows.Add(2284, 49891, '싣');
            table.Rows.Add(2285, 49892, '실');
            table.Rows.Add(2286, 49899, '싫');
            table.Rows.Add(2287, 49900, '심');
            table.Rows.Add(2288, 49901, '십');
            table.Rows.Add(2289, 49903, '싯');
            table.Rows.Add(2290, 49905, '싱');
            table.Rows.Add(2291, 49910, '싶');
            table.Rows.Add(2292, 49912, '싸');
            table.Rows.Add(2293, 49913, '싹');
            table.Rows.Add(2294, 49915, '싻');
            table.Rows.Add(2295, 49916, '싼');
            table.Rows.Add(2296, 49920, '쌀');
            table.Rows.Add(2297, 49928, '쌈');
            table.Rows.Add(2298, 49929, '쌉');
            table.Rows.Add(2299, 49932, '쌌');
            table.Rows.Add(2300, 49933, '쌍');
            table.Rows.Add(2301, 49939, '쌓');
            table.Rows.Add(2302, 49940, '쌔');
            table.Rows.Add(2303, 49941, '쌕');
            table.Rows.Add(2304, 49944, '쌘');
            table.Rows.Add(2305, 49948, '쌜');
            table.Rows.Add(2306, 49956, '쌤');
            table.Rows.Add(2307, 49957, '쌥');
            table.Rows.Add(2308, 49960, '쌨');
            table.Rows.Add(2309, 49961, '쌩');
            table.Rows.Add(2310, 49989, '썅');
            table.Rows.Add(2311, 50024, '써');
            table.Rows.Add(2312, 50025, '썩');
            table.Rows.Add(2313, 50028, '썬');
            table.Rows.Add(2314, 50032, '썰');
            table.Rows.Add(2315, 50034, '썲');
            table.Rows.Add(2316, 50040, '썸');
            table.Rows.Add(2317, 50041, '썹');
            table.Rows.Add(2318, 50044, '썼');
            table.Rows.Add(2319, 50045, '썽');
            table.Rows.Add(2320, 50052, '쎄');
            table.Rows.Add(2321, 50056, '쎈');
            table.Rows.Add(2322, 50060, '쎌');
            table.Rows.Add(2323, 50112, '쏀');
            table.Rows.Add(2324, 50136, '쏘');
            table.Rows.Add(2325, 50137, '쏙');
            table.Rows.Add(2326, 50140, '쏜');
            table.Rows.Add(2327, 50143, '쏟');
            table.Rows.Add(2328, 50144, '쏠');
            table.Rows.Add(2329, 50146, '쏢');
            table.Rows.Add(2330, 50152, '쏨');
            table.Rows.Add(2331, 50153, '쏩');
            table.Rows.Add(2332, 50157, '쏭');
            table.Rows.Add(2333, 50164, '쏴');
            table.Rows.Add(2334, 50165, '쏵');
            table.Rows.Add(2335, 50168, '쏸');
            table.Rows.Add(2336, 50184, '쐈');
            table.Rows.Add(2337, 50192, '쐐');
            table.Rows.Add(2338, 50212, '쐤');
            table.Rows.Add(2339, 50220, '쐬');
            table.Rows.Add(2340, 50224, '쐰');
            table.Rows.Add(2341, 50228, '쐴');
            table.Rows.Add(2342, 50236, '쐼');
            table.Rows.Add(2343, 50237, '쐽');
            table.Rows.Add(2344, 50248, '쑈');
            table.Rows.Add(2345, 50276, '쑤');
            table.Rows.Add(2346, 50277, '쑥');
            table.Rows.Add(2347, 50280, '쑨');
            table.Rows.Add(2348, 50284, '쑬');
            table.Rows.Add(2349, 50292, '쑴');
            table.Rows.Add(2350, 50293, '쑵');
            table.Rows.Add(2351, 50297, '쑹');
            table.Rows.Add(2352, 50304, '쒀');
            table.Rows.Add(2353, 50324, '쒔');
            table.Rows.Add(2354, 50332, '쒜');
            table.Rows.Add(2355, 50360, '쒸');
            table.Rows.Add(2356, 50364, '쒼');
            table.Rows.Add(2357, 50409, '쓩');
            table.Rows.Add(2358, 50416, '쓰');
            table.Rows.Add(2359, 50417, '쓱');
            table.Rows.Add(2360, 50420, '쓴');
            table.Rows.Add(2361, 50424, '쓸');
            table.Rows.Add(2362, 50426, '쓺');
            table.Rows.Add(2363, 50431, '쓿');
            table.Rows.Add(2364, 50432, '씀');
            table.Rows.Add(2365, 50433, '씁');
            table.Rows.Add(2366, 50444, '씌');
            table.Rows.Add(2367, 50448, '씐');
            table.Rows.Add(2368, 50452, '씔');
            table.Rows.Add(2369, 50460, '씜');
            table.Rows.Add(2370, 50472, '씨');
            table.Rows.Add(2371, 50473, '씩');
            table.Rows.Add(2372, 50476, '씬');
            table.Rows.Add(2373, 50480, '씰');
            table.Rows.Add(2374, 50488, '씸');
            table.Rows.Add(2375, 50489, '씹');
            table.Rows.Add(2376, 50491, '씻');
            table.Rows.Add(2377, 50493, '씽');
            table.Rows.Add(2378, 50500, '아');
            table.Rows.Add(2379, 50501, '악');
            table.Rows.Add(2380, 50504, '안');
            table.Rows.Add(2381, 50505, '앉');
            table.Rows.Add(2382, 50506, '않');
            table.Rows.Add(2383, 50508, '알');
            table.Rows.Add(2384, 50509, '앍');
            table.Rows.Add(2385, 50510, '앎');
            table.Rows.Add(2386, 50515, '앓');
            table.Rows.Add(2387, 50516, '암');
            table.Rows.Add(2388, 50517, '압');
            table.Rows.Add(2389, 50519, '앗');
            table.Rows.Add(2390, 50520, '았');
            table.Rows.Add(2391, 50521, '앙');
            table.Rows.Add(2392, 50525, '앝');
            table.Rows.Add(2393, 50526, '앞');
            table.Rows.Add(2394, 50528, '애');
            table.Rows.Add(2395, 50529, '액');
            table.Rows.Add(2396, 50532, '앤');
            table.Rows.Add(2397, 50536, '앨');
            table.Rows.Add(2398, 50544, '앰');
            table.Rows.Add(2399, 50545, '앱');
            table.Rows.Add(2400, 50547, '앳');
            table.Rows.Add(2401, 50548, '앴');
            table.Rows.Add(2402, 50549, '앵');
            table.Rows.Add(2403, 50556, '야');
            table.Rows.Add(2404, 50557, '약');
            table.Rows.Add(2405, 50560, '얀');
            table.Rows.Add(2406, 50564, '얄');
            table.Rows.Add(2407, 50567, '얇');
            table.Rows.Add(2408, 50572, '얌');
            table.Rows.Add(2409, 50573, '얍');
            table.Rows.Add(2410, 50575, '얏');
            table.Rows.Add(2411, 50577, '양');
            table.Rows.Add(2412, 50581, '얕');
            table.Rows.Add(2413, 50583, '얗');
            table.Rows.Add(2414, 50584, '얘');
            table.Rows.Add(2415, 50588, '얜');
            table.Rows.Add(2416, 50592, '얠');
            table.Rows.Add(2417, 50601, '얩');
            table.Rows.Add(2418, 50612, '어');
            table.Rows.Add(2419, 50613, '억');
            table.Rows.Add(2420, 50616, '언');
            table.Rows.Add(2421, 50617, '얹');
            table.Rows.Add(2422, 50619, '얻');
            table.Rows.Add(2423, 50620, '얼');
            table.Rows.Add(2424, 50621, '얽');
            table.Rows.Add(2425, 50622, '얾');
            table.Rows.Add(2426, 50628, '엄');
            table.Rows.Add(2427, 50629, '업');
            table.Rows.Add(2428, 50630, '없');
            table.Rows.Add(2429, 50631, '엇');
            table.Rows.Add(2430, 50632, '었');
            table.Rows.Add(2431, 50633, '엉');
            table.Rows.Add(2432, 50634, '엊');
            table.Rows.Add(2433, 50636, '엌');
            table.Rows.Add(2434, 50638, '엎');
            table.Rows.Add(2435, 50640, '에');
            table.Rows.Add(2436, 50641, '엑');
            table.Rows.Add(2437, 50644, '엔');
            table.Rows.Add(2438, 50648, '엘');
            table.Rows.Add(2439, 50656, '엠');
            table.Rows.Add(2440, 50657, '엡');
            table.Rows.Add(2441, 50659, '엣');
            table.Rows.Add(2442, 50661, '엥');
            table.Rows.Add(2443, 50668, '여');
            table.Rows.Add(2444, 50669, '역');
            table.Rows.Add(2445, 50670, '엮');
            table.Rows.Add(2446, 50672, '연');
            table.Rows.Add(2447, 50676, '열');
            table.Rows.Add(2448, 50678, '엶');
            table.Rows.Add(2449, 50679, '엷');
            table.Rows.Add(2450, 50684, '염');
            table.Rows.Add(2451, 50685, '엽');
            table.Rows.Add(2452, 50686, '엾');
            table.Rows.Add(2453, 50687, '엿');
            table.Rows.Add(2454, 50688, '였');
            table.Rows.Add(2455, 50689, '영');
            table.Rows.Add(2456, 50693, '옅');
            table.Rows.Add(2457, 50694, '옆');
            table.Rows.Add(2458, 50695, '옇');
            table.Rows.Add(2459, 50696, '예');
            table.Rows.Add(2460, 50700, '옌');
            table.Rows.Add(2461, 50704, '옐');
            table.Rows.Add(2462, 50712, '옘');
            table.Rows.Add(2463, 50713, '옙');
            table.Rows.Add(2464, 50715, '옛');
            table.Rows.Add(2465, 50716, '옜');
            table.Rows.Add(2466, 50724, '오');
            table.Rows.Add(2467, 50725, '옥');
            table.Rows.Add(2468, 50728, '온');
            table.Rows.Add(2469, 50732, '올');
            table.Rows.Add(2470, 50733, '옭');
            table.Rows.Add(2471, 50734, '옮');
            table.Rows.Add(2472, 50736, '옰');
            table.Rows.Add(2473, 50739, '옳');
            table.Rows.Add(2474, 50740, '옴');
            table.Rows.Add(2475, 50741, '옵');
            table.Rows.Add(2476, 50743, '옷');
            table.Rows.Add(2477, 50745, '옹');
            table.Rows.Add(2478, 50747, '옻');
            table.Rows.Add(2479, 50752, '와');
            table.Rows.Add(2480, 50753, '왁');
            table.Rows.Add(2481, 50756, '완');
            table.Rows.Add(2482, 50760, '왈');
            table.Rows.Add(2483, 50768, '왐');
            table.Rows.Add(2484, 50769, '왑');
            table.Rows.Add(2485, 50771, '왓');
            table.Rows.Add(2486, 50772, '왔');
            table.Rows.Add(2487, 50773, '왕');
            table.Rows.Add(2488, 50780, '왜');
            table.Rows.Add(2489, 50781, '왝');
            table.Rows.Add(2490, 50784, '왠');
            table.Rows.Add(2491, 50796, '왬');
            table.Rows.Add(2492, 50799, '왯');
            table.Rows.Add(2493, 50801, '왱');
            table.Rows.Add(2494, 50808, '외');
            table.Rows.Add(2495, 50809, '왹');
            table.Rows.Add(2496, 50812, '왼');
            table.Rows.Add(2497, 50816, '욀');
            table.Rows.Add(2498, 50824, '욈');
            table.Rows.Add(2499, 50825, '욉');
            table.Rows.Add(2500, 50827, '욋');
            table.Rows.Add(2501, 50829, '욍');
            table.Rows.Add(2502, 50836, '요');
            table.Rows.Add(2503, 50837, '욕');
            table.Rows.Add(2504, 50840, '욘');
            table.Rows.Add(2505, 50844, '욜');
            table.Rows.Add(2506, 50852, '욤');
            table.Rows.Add(2507, 50853, '욥');
            table.Rows.Add(2508, 50855, '욧');
            table.Rows.Add(2509, 50857, '용');
            table.Rows.Add(2510, 50864, '우');
            table.Rows.Add(2511, 50865, '욱');
            table.Rows.Add(2512, 50868, '운');
            table.Rows.Add(2513, 50872, '울');
            table.Rows.Add(2514, 50873, '욹');
            table.Rows.Add(2515, 50874, '욺');
            table.Rows.Add(2516, 50880, '움');
            table.Rows.Add(2517, 50881, '웁');
            table.Rows.Add(2518, 50883, '웃');
            table.Rows.Add(2519, 50885, '웅');
            table.Rows.Add(2520, 50892, '워');
            table.Rows.Add(2521, 50893, '웍');
            table.Rows.Add(2522, 50896, '원');
            table.Rows.Add(2523, 50900, '월');
            table.Rows.Add(2524, 50908, '웜');
            table.Rows.Add(2525, 50909, '웝');
            table.Rows.Add(2526, 50912, '웠');
            table.Rows.Add(2527, 50913, '웡');
            table.Rows.Add(2528, 50920, '웨');
            table.Rows.Add(2529, 50921, '웩');
            table.Rows.Add(2530, 50924, '웬');
            table.Rows.Add(2531, 50928, '웰');
            table.Rows.Add(2532, 50936, '웸');
            table.Rows.Add(2533, 50937, '웹');
            table.Rows.Add(2534, 50941, '웽');
            table.Rows.Add(2535, 50948, '위');
            table.Rows.Add(2536, 50949, '윅');
            table.Rows.Add(2537, 50952, '윈');
            table.Rows.Add(2538, 50956, '윌');
            table.Rows.Add(2539, 50964, '윔');
            table.Rows.Add(2540, 50965, '윕');
            table.Rows.Add(2541, 50967, '윗');
            table.Rows.Add(2542, 50969, '윙');
            table.Rows.Add(2543, 50976, '유');
            table.Rows.Add(2544, 50977, '육');
            table.Rows.Add(2545, 50980, '윤');
            table.Rows.Add(2546, 50984, '율');
            table.Rows.Add(2547, 50992, '윰');
            table.Rows.Add(2548, 50993, '윱');
            table.Rows.Add(2549, 50995, '윳');
            table.Rows.Add(2550, 50997, '융');
            table.Rows.Add(2551, 50999, '윷');
            table.Rows.Add(2552, 51004, '으');
            table.Rows.Add(2553, 51005, '윽');
            table.Rows.Add(2554, 51008, '은');
            table.Rows.Add(2555, 51012, '을');
            table.Rows.Add(2556, 51018, '읊');
            table.Rows.Add(2557, 51020, '음');
            table.Rows.Add(2558, 51021, '읍');
            table.Rows.Add(2559, 51023, '읏');
            table.Rows.Add(2560, 51025, '응');
            table.Rows.Add(2561, 51026, '읒');
            table.Rows.Add(2562, 51027, '읓');
            table.Rows.Add(2563, 51028, '읔');
            table.Rows.Add(2564, 51029, '읕');
            table.Rows.Add(2565, 51030, '읖');
            table.Rows.Add(2566, 51031, '읗');
            table.Rows.Add(2567, 51032, '의');
            table.Rows.Add(2568, 51036, '읜');
            table.Rows.Add(2569, 51040, '읠');
            table.Rows.Add(2570, 51048, '읨');
            table.Rows.Add(2571, 51051, '읫');
            table.Rows.Add(2572, 51060, '이');
            table.Rows.Add(2573, 51061, '익');
            table.Rows.Add(2574, 51064, '인');
            table.Rows.Add(2575, 51068, '일');
            table.Rows.Add(2576, 51069, '읽');
            table.Rows.Add(2577, 51070, '읾');
            table.Rows.Add(2578, 51075, '잃');
            table.Rows.Add(2579, 51076, '임');
            table.Rows.Add(2580, 51077, '입');
            table.Rows.Add(2581, 51079, '잇');
            table.Rows.Add(2582, 51080, '있');
            table.Rows.Add(2583, 51081, '잉');
            table.Rows.Add(2584, 51082, '잊');
            table.Rows.Add(2585, 51086, '잎');
            table.Rows.Add(2586, 51088, '자');
            table.Rows.Add(2587, 51089, '작');
            table.Rows.Add(2588, 51092, '잔');
            table.Rows.Add(2589, 51094, '잖');
            table.Rows.Add(2590, 51095, '잗');
            table.Rows.Add(2591, 51096, '잘');
            table.Rows.Add(2592, 51098, '잚');
            table.Rows.Add(2593, 51104, '잠');
            table.Rows.Add(2594, 51105, '잡');
            table.Rows.Add(2595, 51107, '잣');
            table.Rows.Add(2596, 51108, '잤');
            table.Rows.Add(2597, 51109, '장');
            table.Rows.Add(2598, 51110, '잦');
            table.Rows.Add(2599, 51116, '재');
            table.Rows.Add(2600, 51117, '잭');
            table.Rows.Add(2601, 51120, '잰');
            table.Rows.Add(2602, 51124, '잴');
            table.Rows.Add(2603, 51132, '잼');
            table.Rows.Add(2604, 51133, '잽');
            table.Rows.Add(2605, 51135, '잿');
            table.Rows.Add(2606, 51136, '쟀');
            table.Rows.Add(2607, 51137, '쟁');
            table.Rows.Add(2608, 51144, '쟈');
            table.Rows.Add(2609, 51145, '쟉');
            table.Rows.Add(2610, 51148, '쟌');
            table.Rows.Add(2611, 51150, '쟎');
            table.Rows.Add(2612, 51152, '쟐');
            table.Rows.Add(2613, 51160, '쟘');
            table.Rows.Add(2614, 51165, '쟝');
            table.Rows.Add(2615, 51172, '쟤');
            table.Rows.Add(2616, 51176, '쟨');
            table.Rows.Add(2617, 51180, '쟬');
            table.Rows.Add(2618, 51200, '저');
            table.Rows.Add(2619, 51201, '적');
            table.Rows.Add(2620, 51204, '전');
            table.Rows.Add(2621, 51208, '절');
            table.Rows.Add(2622, 51210, '젊');
            table.Rows.Add(2623, 51216, '점');
            table.Rows.Add(2624, 51217, '접');
            table.Rows.Add(2625, 51219, '젓');
            table.Rows.Add(2626, 51221, '정');
            table.Rows.Add(2627, 51222, '젖');
            table.Rows.Add(2628, 51228, '제');
            table.Rows.Add(2629, 51229, '젝');
            table.Rows.Add(2630, 51232, '젠');
            table.Rows.Add(2631, 51236, '젤');
            table.Rows.Add(2632, 51244, '젬');
            table.Rows.Add(2633, 51245, '젭');
            table.Rows.Add(2634, 51247, '젯');
            table.Rows.Add(2635, 51249, '젱');
            table.Rows.Add(2636, 51256, '져');
            table.Rows.Add(2637, 51260, '젼');
            table.Rows.Add(2638, 51264, '졀');
            table.Rows.Add(2639, 51272, '졈');
            table.Rows.Add(2640, 51273, '졉');
            table.Rows.Add(2641, 51276, '졌');
            table.Rows.Add(2642, 51277, '졍');
            table.Rows.Add(2643, 51284, '졔');
            table.Rows.Add(2644, 51312, '조');
            table.Rows.Add(2645, 51313, '족');
            table.Rows.Add(2646, 51316, '존');
            table.Rows.Add(2647, 51320, '졸');
            table.Rows.Add(2648, 51322, '졺');
            table.Rows.Add(2649, 51328, '좀');
            table.Rows.Add(2650, 51329, '좁');
            table.Rows.Add(2651, 51331, '좃');
            table.Rows.Add(2652, 51333, '종');
            table.Rows.Add(2653, 51334, '좆');
            table.Rows.Add(2654, 51335, '좇');
            table.Rows.Add(2655, 51339, '좋');
            table.Rows.Add(2656, 51340, '좌');
            table.Rows.Add(2657, 51341, '좍');
            table.Rows.Add(2658, 51348, '좔');
            table.Rows.Add(2659, 51357, '좝');
            table.Rows.Add(2660, 51359, '좟');
            table.Rows.Add(2661, 51361, '좡');
            table.Rows.Add(2662, 51368, '좨');
            table.Rows.Add(2663, 51388, '좼');
            table.Rows.Add(2664, 51389, '좽');
            table.Rows.Add(2665, 51396, '죄');
            table.Rows.Add(2666, 51400, '죈');
            table.Rows.Add(2667, 51404, '죌');
            table.Rows.Add(2668, 51412, '죔');
            table.Rows.Add(2669, 51413, '죕');
            table.Rows.Add(2670, 51415, '죗');
            table.Rows.Add(2671, 51417, '죙');
            table.Rows.Add(2672, 51424, '죠');
            table.Rows.Add(2673, 51425, '죡');
            table.Rows.Add(2674, 51428, '죤');
            table.Rows.Add(2675, 51445, '죵');
            table.Rows.Add(2676, 51452, '주');
            table.Rows.Add(2677, 51453, '죽');
            table.Rows.Add(2678, 51456, '준');
            table.Rows.Add(2679, 51460, '줄');
            table.Rows.Add(2680, 51461, '줅');
            table.Rows.Add(2681, 51462, '줆');
            table.Rows.Add(2682, 51468, '줌');
            table.Rows.Add(2683, 51469, '줍');
            table.Rows.Add(2684, 51471, '줏');
            table.Rows.Add(2685, 51473, '중');
            table.Rows.Add(2686, 51480, '줘');
            table.Rows.Add(2687, 51500, '줬');
            table.Rows.Add(2688, 51508, '줴');
            table.Rows.Add(2689, 51536, '쥐');
            table.Rows.Add(2690, 51537, '쥑');
            table.Rows.Add(2691, 51540, '쥔');
            table.Rows.Add(2692, 51544, '쥘');
            table.Rows.Add(2693, 51552, '쥠');
            table.Rows.Add(2694, 51553, '쥡');
            table.Rows.Add(2695, 51555, '쥣');
            table.Rows.Add(2696, 51564, '쥬');
            table.Rows.Add(2697, 51568, '쥰');
            table.Rows.Add(2698, 51572, '쥴');
            table.Rows.Add(2699, 51580, '쥼');
            table.Rows.Add(2700, 51592, '즈');
            table.Rows.Add(2701, 51593, '즉');
            table.Rows.Add(2702, 51596, '즌');
            table.Rows.Add(2703, 51600, '즐');
            table.Rows.Add(2704, 51608, '즘');
            table.Rows.Add(2705, 51609, '즙');
            table.Rows.Add(2706, 51611, '즛');
            table.Rows.Add(2707, 51613, '증');
            table.Rows.Add(2708, 51648, '지');
            table.Rows.Add(2709, 51649, '직');
            table.Rows.Add(2710, 51652, '진');
            table.Rows.Add(2711, 51655, '짇');
            table.Rows.Add(2712, 51656, '질');
            table.Rows.Add(2713, 51658, '짊');
            table.Rows.Add(2714, 51664, '짐');
            table.Rows.Add(2715, 51665, '집');
            table.Rows.Add(2716, 51667, '짓');
            table.Rows.Add(2717, 51669, '징');
            table.Rows.Add(2718, 51670, '짖');
            table.Rows.Add(2719, 51673, '짙');
            table.Rows.Add(2720, 51674, '짚');
            table.Rows.Add(2721, 51676, '짜');
            table.Rows.Add(2722, 51677, '짝');
            table.Rows.Add(2723, 51680, '짠');
            table.Rows.Add(2724, 51682, '짢');
            table.Rows.Add(2725, 51684, '짤');
            table.Rows.Add(2726, 51687, '짧');
            table.Rows.Add(2727, 51692, '짬');
            table.Rows.Add(2728, 51693, '짭');
            table.Rows.Add(2729, 51695, '짯');
            table.Rows.Add(2730, 51696, '짰');
            table.Rows.Add(2731, 51697, '짱');
            table.Rows.Add(2732, 51704, '째');
            table.Rows.Add(2733, 51705, '짹');
            table.Rows.Add(2734, 51708, '짼');
            table.Rows.Add(2735, 51712, '쨀');
            table.Rows.Add(2736, 51720, '쨈');
            table.Rows.Add(2737, 51721, '쨉');
            table.Rows.Add(2738, 51723, '쨋');
            table.Rows.Add(2739, 51724, '쨌');
            table.Rows.Add(2740, 51725, '쨍');
            table.Rows.Add(2741, 51732, '쨔');
            table.Rows.Add(2742, 51736, '쨘');
            table.Rows.Add(2743, 51753, '쨩');
            table.Rows.Add(2744, 51788, '쩌');
            table.Rows.Add(2745, 51789, '쩍');
            table.Rows.Add(2746, 51792, '쩐');
            table.Rows.Add(2747, 51796, '쩔');
            table.Rows.Add(2748, 51804, '쩜');
            table.Rows.Add(2749, 51805, '쩝');
            table.Rows.Add(2750, 51807, '쩟');
            table.Rows.Add(2751, 51808, '쩠');
            table.Rows.Add(2752, 51809, '쩡');
            table.Rows.Add(2753, 51816, '쩨');
            table.Rows.Add(2754, 51837, '쩽');
            table.Rows.Add(2755, 51844, '쪄');
            table.Rows.Add(2756, 51864, '쪘');
            table.Rows.Add(2757, 51900, '쪼');
            table.Rows.Add(2758, 51901, '쪽');
            table.Rows.Add(2759, 51904, '쫀');
            table.Rows.Add(2760, 51908, '쫄');
            table.Rows.Add(2761, 51916, '쫌');
            table.Rows.Add(2762, 51917, '쫍');
            table.Rows.Add(2763, 51919, '쫏');
            table.Rows.Add(2764, 51921, '쫑');
            table.Rows.Add(2765, 51923, '쫓');
            table.Rows.Add(2766, 51928, '쫘');
            table.Rows.Add(2767, 51929, '쫙');
            table.Rows.Add(2768, 51936, '쫠');
            table.Rows.Add(2769, 51948, '쫬');
            table.Rows.Add(2770, 51956, '쫴');
            table.Rows.Add(2771, 51976, '쬈');
            table.Rows.Add(2772, 51984, '쬐');
            table.Rows.Add(2773, 51988, '쬔');
            table.Rows.Add(2774, 51992, '쬘');
            table.Rows.Add(2775, 52000, '쬠');
            table.Rows.Add(2776, 52001, '쬡');
            table.Rows.Add(2777, 52033, '쭁');
            table.Rows.Add(2778, 52040, '쭈');
            table.Rows.Add(2779, 52041, '쭉');
            table.Rows.Add(2780, 52044, '쭌');
            table.Rows.Add(2781, 52048, '쭐');
            table.Rows.Add(2782, 52056, '쭘');
            table.Rows.Add(2783, 52057, '쭙');
            table.Rows.Add(2784, 52061, '쭝');
            table.Rows.Add(2785, 52068, '쭤');
            table.Rows.Add(2786, 52088, '쭸');
            table.Rows.Add(2787, 52089, '쭹');
            table.Rows.Add(2788, 52124, '쮜');
            table.Rows.Add(2789, 52152, '쮸');
            table.Rows.Add(2790, 52180, '쯔');
            table.Rows.Add(2791, 52196, '쯤');
            table.Rows.Add(2792, 52199, '쯧');
            table.Rows.Add(2793, 52201, '쯩');
            table.Rows.Add(2794, 52236, '찌');
            table.Rows.Add(2795, 52237, '찍');
            table.Rows.Add(2796, 52240, '찐');
            table.Rows.Add(2797, 52244, '찔');
            table.Rows.Add(2798, 52252, '찜');
            table.Rows.Add(2799, 52253, '찝');
            table.Rows.Add(2800, 52257, '찡');
            table.Rows.Add(2801, 52258, '찢');
            table.Rows.Add(2802, 52263, '찧');
            table.Rows.Add(2803, 52264, '차');
            table.Rows.Add(2804, 52265, '착');
            table.Rows.Add(2805, 52268, '찬');
            table.Rows.Add(2806, 52270, '찮');
            table.Rows.Add(2807, 52272, '찰');
            table.Rows.Add(2808, 52280, '참');
            table.Rows.Add(2809, 52281, '찹');
            table.Rows.Add(2810, 52283, '찻');
            table.Rows.Add(2811, 52284, '찼');
            table.Rows.Add(2812, 52285, '창');
            table.Rows.Add(2813, 52286, '찾');
            table.Rows.Add(2814, 52292, '채');
            table.Rows.Add(2815, 52293, '책');
            table.Rows.Add(2816, 52296, '챈');
            table.Rows.Add(2817, 52300, '챌');
            table.Rows.Add(2818, 52308, '챔');
            table.Rows.Add(2819, 52309, '챕');
            table.Rows.Add(2820, 52311, '챗');
            table.Rows.Add(2821, 52312, '챘');
            table.Rows.Add(2822, 52313, '챙');
            table.Rows.Add(2823, 52320, '챠');
            table.Rows.Add(2824, 52324, '챤');
            table.Rows.Add(2825, 52326, '챦');
            table.Rows.Add(2826, 52328, '챨');
            table.Rows.Add(2827, 52336, '챰');
            table.Rows.Add(2828, 52341, '챵');
            table.Rows.Add(2829, 52376, '처');
            table.Rows.Add(2830, 52377, '척');
            table.Rows.Add(2831, 52380, '천');
            table.Rows.Add(2832, 52384, '철');
            table.Rows.Add(2833, 52392, '첨');
            table.Rows.Add(2834, 52393, '첩');
            table.Rows.Add(2835, 52395, '첫');
            table.Rows.Add(2836, 52396, '첬');
            table.Rows.Add(2837, 52397, '청');
            table.Rows.Add(2838, 52404, '체');
            table.Rows.Add(2839, 52405, '첵');
            table.Rows.Add(2840, 52408, '첸');
            table.Rows.Add(2841, 52412, '첼');
            table.Rows.Add(2842, 52420, '쳄');
            table.Rows.Add(2843, 52421, '쳅');
            table.Rows.Add(2844, 52423, '쳇');
            table.Rows.Add(2845, 52425, '쳉');
            table.Rows.Add(2846, 52432, '쳐');
            table.Rows.Add(2847, 52436, '쳔');
            table.Rows.Add(2848, 52452, '쳤');
            table.Rows.Add(2849, 52460, '쳬');
            table.Rows.Add(2850, 52464, '쳰');
            table.Rows.Add(2851, 52481, '촁');
            #endregion
            return table;
        }
        
        public string[] items = {null,
"Master Ball",
"Ultra Ball",
"Great Ball",
"Poké Ball",
"Safari Ball",
"Net Ball",
"Dive Ball",
"Nest Ball",
"Repeat Ball",
"Timer Ball",
"Luxury Ball",
"Premier Ball",
"Dusk Ball",
"Heal Ball",
"Quick Ball",
"Cherish Ball",
"Potion",
"Antidote",
"Burn Heal",
"Ice Heal",
"Awakening",
"Parlyz Heal",
"Full Restore",
"Max Potion",
"Hyper Potion",
"Super Potion",
"Full Heal",
"Revive",
"Max Revive",
"Fresh Water",
"Soda Pop",
"Lemonade",
"Moomoo Milk",
"EnergyPowder",
"Energy Root",
"Heal Powder",
"Revival Herb",
"Ether",
"Max Ether",
"Elixir",
"Max Elixir",
"Lava Cookie",
"Berry Juice",
"Sacred Ash",
"HP Up",
"Protein",
"Iron",
"Carbos",
"Calcium",
"Rare Candy",
"PP Up",
"Zinc",
"PP Max",
"Old Gateau",
"Guard Spec.",
"Dire Hit",
"X Attack",
"X Defend",
"X Speed",
"X Accuracy",
"X Special",
"X Sp. Def",
"Poké Doll",
"Fluffy Tail",
"Blue Flute",
"Yellow Flute",
"Red Flute",
"Black Flute",
"White Flute",
"Shoal Salt",
"Shoal Shell",
"Red Shard",
"Blue Shard",
"Yellow Shard",
"Green Shard",
"Super Repel",
"Max Repel",
"Escape Rope",
"Repel",
"Sun Stone",
"Moon Stone",
"Fire Stone",
"Thunder Stone",
"Water Stone",
"Leaf Stone",
"TinyMushroom",
"Big Mushroom",
"Pearl",
"Big Pearl",
"Stardust",
"Star Piece",
"Nugget",
"Heart Scale",
"Honey",
"Growth Mulch",
"Damp Mulch",
"Stable Mulch",
"Gooey Mulch",
"Root Fossil",
"Claw Fossil",
"Helix Fossil",
"Dome Fossil",
"Old Amber",
"Armor Fossil",
"Skull Fossil",
"Rare Bone",
"Shiny Stone",
"Dusk Stone",
"Dawn Stone",
"Oval Stone",
"Odd Keystone",
"Griseous Orb",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"unknown",
"Adamant Orb",
"Lustrous Orb",
"Grass Mail",
"Flame Mail",
"Bubble Mail",
"Bloom Mail",
"Tunnel Mail",
"Steel Mail",
"Heart Mail",
"Snow Mail",
"Space Mail",
"Air Mail",
"Mosaic Mail",
"Brick Mail",
"Cheri Berry",
"Chesto Berry",
"Pecha Berry",
"Rawst Berry",
"Aspear Berry",
"Leppa Berry",
"Oran Berry",
"Persim Berry",
"Lum Berry",
"Sitrus Berry",
"Figy Berry",
"Wiki Berry",
"Mago Berry",
"Aguav Berry",
"Iapapa Berry",
"Razz Berry",
"Bluk Berry",
"Nanab Berry",
"Wepear Berry",
"Pinap Berry",
"Pomeg Berry",
"Kelpsy Berry",
"Qualot Berry",
"Hondew Berry",
"Grepa Berry",
"Tamato Berry",
"Cornn Berry",
"Magost Berry",
"Rabuta Berry",
"Nomel Berry",
"Spelon Berry",
"Pamtre Berry",
"Watmel Berry",
"Durin Berry",
"Belue Berry",
"Occa Berry",
"Passho Berry",
"Wacan Berry",
"Rindo Berry",
"Yache Berry",
"Chople Berry",
"Kebia Berry",
"Shuca Berry",
"Coba Berry",
"Payapa Berry",
"Tanga Berry",
"Charti Berry",
"Kasib Berry",
"Haban Berry",
"Colbur Berry",
"Babiri Berry",
"Chilan Berry",
"Liechi Berry",
"Ganlon Berry",
"Salac Berry",
"Petaya Berry",
"Apicot Berry",
"Lansat Berry",
"Starf Berry",
"Enigma Berry",
"Micle Berry",
"Custap Berry",
"Jaboca Berry",
"Rowap Berry",
"BrightPowder",
"White Herb",
"Macho Brace",
"Exp. Share",
"Quick Claw",
"Soothe Bell",
"Mental Herb",
"Choice Band",
"King's Rock",
"SilverPowder",
"Amulet Coin",
"Cleanse Tag",
"Soul Dew",
"DeepSeaTooth",
"DeepSeaScale",
"Smoke Ball",
"Everstone",
"Focus Band",
"Lucky Egg",
"Scope Lens",
"Metal Coat",
"Leftovers",
"Dragon Scale",
"Light Ball",
"Soft Sand",
"Hard Stone",
"Miracle Seed",
"BlackGlasses",
"Black Belt",
"Magnet",
"Mystic Water",
"Sharp Beak",
"Poison Barb",
"NeverMeltIce",
"Spell Tag",
"TwistedSpoon",
"Charcoal",
"Dragon Fang",
"Silk Scarf",
"Up-Grade",
"Shell Bell",
"Sea Incense",
"Lax Incense",
"Lucky Punch",
"Metal Powder",
"Thick Club",
"Stick",
"Red Scarf",
"Blue Scarf",
"Pink Scarf",
"Green Scarf",
"Yellow Scarf",
"Wide Lens",
"Muscle Band",
"Wise Glasses",
"Expert Belt",
"Light Clay",
"Life Orb",
"Power Herb",
"Toxic Orb",
"Flame Orb",
"Quick Powder",
"Focus Sash",
"Zoom Lens",
"Metronome",
"Iron Ball",
"Lagging Tail",
"Destiny Knot",
"Black Sludge",
"Icy Rock",
"Smooth Rock",
"Heat Rock",
"Damp Rock",
"Grip Claw",
"Choice Scarf",
"Sticky Barb",
"Power Bracer",
"Power Belt",
"Power Lens",
"Power Band",
"Power Anklet",
"Power Weight",
"Shed Shell",
"Big Root",
"Choice Specs",
"Flame Plate",
"Splash Plate",
"Zap Plate",
"Meadow Plate",
"Icicle Plate",
"Fist Plate",
"Toxic Plate",
"Earth Plate",
"Sky Plate",
"Mind Plate",
"Insect Plate",
"Stone Plate",
"Spooky Plate",
"Draco Plate",
"Dread Plate",
"Iron Plate",
"Odd Incense",
"Rock Incense",
"Full Incense",
"Wave Incense",
"Rose Incense",
"Luck Incense",
"Pure Incense",
"Protector",
"Electirizer",
"Magmarizer",
"Dubious Disc",
"Reaper Cloth",
"Razor Claw",
"Razor Fang",
"TM01",
"TM02",
"TM03",
"TM04",
"TM05",
"TM06",
"TM07",
"TM08",
"TM09",
"TM10",
"TM11",
"TM12",
"TM13",
"TM14",
"TM15",
"TM16",
"TM17",
"TM18",
"TM19",
"TM20",
"TM21",
"TM22",
"TM23",
"TM24",
"TM25",
"TM26",
"TM27",
"TM28",
"TM29",
"TM30",
"TM31",
"TM32",
"TM33",
"TM34",
"TM35",
"TM36",
"TM37",
"TM38",
"TM39",
"TM40",
"TM41",
"TM42",
"TM43",
"TM44",
"TM45",
"TM46",
"TM47",
"TM48",
"TM49",
"TM50",
"TM51",
"TM52",
"TM53",
"TM54",
"TM55",
"TM56",
"TM57",
"TM58",
"TM59",
"TM60",
"TM61",
"TM62",
"TM63",
"TM64",
"TM65",
"TM66",
"TM67",
"TM68",
"TM69",
"TM70",
"TM71",
"TM72",
"TM73",
"TM74",
"TM75",
"TM76",
"TM77",
"TM78",
"TM79",
"TM80",
"TM81",
"TM82",
"TM83",
"TM84",
"TM85",
"TM86",
"TM87",
"TM88",
"TM89",
"TM90",
"TM91",
"TM92",
"HM01",
"HM02",
"HM03",
"HM04",
"HM05",
"HM06",
"HM07",
"HM08",
"Explorer Kit",
"Loot Sack",
"Rule Book",
"Poké Radar",
"Point Card",
"Journal",
"Seal Case",
"Fashion Case",
"Seal Bag",
"Pal Pad",
"Works Key",
"Old Charm",
"Galactic Key",
"Red Chain",
"Town Map",
"Vs. Seeker",
"Coin Case",
"Old Rod",
"Good Rod",
"Super Rod",
"Sprayduck",
"Poffin Case",
"Bicycle",
"Suite Key",
"Oak's Letter",
"Lunar Wing",
"Member Card",
"Azure Flute",
"S.S. Ticket",
"Contest Pass",
"Magma Stone",
"Parcel",
"Coupon 1",
"Coupon 2",
"Coupon 3",
"Storage Key",
"SecretPotion",
"Vs. Recorder",
"Gracidea",
"Secret Key",
"Apricorn Box",
"Unown Report",
"Berry Pots",
"Dowsing MCHN",
"Blue Card",
"SlowpokeTail",
"Clear Bell",
"Card Key",
"Basement Key",
"SquirtBottle",
"Red Scale",
"Lost Item",
"Pass",
"Machine Part",
"Silver Wing",
"Rainbow Wing",
"Mystery Egg",
"Red Apricorn",
"Ylw Apricorn",
"Blu Apricorn",
"Grn Apricorn",
"Pnk Apricorn",
"Wht Apricorn",
"Blk Apricorn",
"Fast Ball",
"Level Ball",
"Lure Ball",
"Heavy Ball",
"Love Ball",
"Friend Ball",
"Moon Ball",
"Sport Ball",
"Park Ball",
"Photo Album",
"GB Sounds",
"Tidal Bell",
"RageCandyBar",
"Data Card 01",
"Data Card 02",
"Data Card 03",
"Data Card 04",
"Data Card 05",
"Data Card 06",
"Data Card 07",
"Data Card 08",
"Data Card 09",
"Data Card 10",
"Data Card 11",
"Data Card 12",
"Data Card 13",
"Data Card 14",
"Data Card 15",
"Data Card 16",
"Data Card 17",
"Data Card 18",
"Data Card 19",
"Data Card 20",
"Data Card 21",
"Data Card 22",
"Data Card 23",
"Data Card 24",
"Data Card 25",
"Data Card 26",
"Data Card 27",
"Jade Orb",
"Lock Capsule",
"Red Orb",
"Blue Orb",
"Enigma Stone"};

        public string[] specieslist = {
	"---",
	"Bulbasaur",
	"Ivysaur",
	"Venusaur",
	"Charmander",
	"Charmeleon",
	"Charizard",
	"Squirtle",
	"Wartortle",
	"Blastoise",
	"Caterpie",
	"Metapod",
	"Butterfree",
	"Weedle",
	"Kakuna",
	"Beedrill",
	"Pidgey",
	"Pidgeotto",
	"Pidgeot",
	"Rattata",
	"Raticate",
	"Spearow",
	"Fearow",
	"Ekans",
	"Arbok",
	"Pikachu",
	"Raichu",
	"Sandshrew",
	"Sandslash",
	"Nidoran♀",
	"Nidorina",
	"Nidoqueen",
	"Nidoran♂",
	"Nidorino",
	"Nidoking",
	"Clefairy",
	"Clefable",
	"Vulpix",
	"Ninetales",
	"Jigglypuff",
	"Wigglytuff",
	"Zubat",
	"Golbat",
	"Oddish",
	"Gloom",
	"Vileplume",
	"Paras",
	"Parasect",
	"Venonat",
	"Venomoth",
	"Diglett",
	"Dugtrio",
	"Meowth",
	"Persian",
	"Psyduck",
	"Golduck",
	"Mankey",
	"Primeape",
	"Growlithe",
	"Arcanine",
	"Poliwag",
	"Poliwhirl",
	"Poliwrath",
	"Abra",
	"Kadabra",
	"Alakazam",
	"Machop",
	"Machoke",
	"Machamp",
	"Bellsprout",
	"Weepinbell",
	"Victreebel",
	"Tentacool",
	"Tentacruel",
	"Geodude",
	"Graveler",
	"Golem",
	"Ponyta",
	"Rapidash",
	"Slowpoke",
	"Slowbro",
	"Magnemite",
	"Magneton",
	"Farfetch'd",
	"Doduo",
	"Dodrio",
	"Seel",
	"Dewgong",
	"Grimer",
	"Muk",
	"Shellder",
	"Cloyster",
	"Gastly",
	"Haunter",
	"Gengar",
	"Onix",
	"Drowzee",
	"Hypno",
	"Krabby",
	"Kingler",
	"Voltorb",
	"Electrode",
	"Exeggcute",
	"Exeggutor",
	"Cubone",
	"Marowak",
	"Hitmonlee",
	"Hitmonchan",
	"Lickitung",
	"Koffing",
	"Weezing",
	"Rhyhorn",
	"Rhydon",
	"Chansey",
	"Tangela",
	"Kangaskhan",
	"Horsea",
	"Seadra",
	"Goldeen",
	"Seaking",
	"Staryu",
	"Starmie",
	"Mr. Mime",
	"Scyther",
	"Jynx",
	"Electabuzz",
	"Magmar",
	"Pinsir",
	"Tauros",
	"Magikarp",
	"Gyarados",
	"Lapras",
	"Ditto",
	"Eevee",
	"Vaporeon",
	"Jolteon",
	"Flareon",
	"Porygon",
	"Omanyte",
	"Omastar",
	"Kabuto",
	"Kabutops",
	"Aerodactyl",
	"Snorlax",
	"Articuno",
	"Zapdos",
	"Moltres",
	"Dratini",
	"Dragonair",
	"Dragonite",
	"Mewtwo",
	"Mew",
	"Chikorita",
	"Bayleef",
	"Meganium",
	"Cyndaquil",
	"Quilava",
	"Typhlosion",
	"Totodile",
	"Croconaw",
	"Feraligatr",
	"Sentret",
	"Furret",
	"Hoothoot",
	"Noctowl",
	"Ledyba",
	"Ledian",
	"Spinarak",
	"Ariados",
	"Crobat",
	"Chinchou",
	"Lanturn",
	"Pichu",
	"Cleffa",
	"Igglybuff",
	"Togepi",
	"Togetic",
	"Natu",
	"Xatu",
	"Mareep",
	"Flaaffy",
	"Ampharos",
	"Bellossom",
	"Marill",
	"Azumarill",
	"Sudowoodo",
	"Politoed",
	"Hoppip",
	"Skiploom",
	"Jumpluff",
	"Aipom",
	"Sunkern",
	"Sunflora",
	"Yanma",
	"Wooper",
	"Quagsire",
	"Espeon",
	"Umbreon",
	"Murkrow",
	"Slowking",
	"Misdreavus",
	"Unown",
	"Wobbuffet",
	"Girafarig",
	"Pineco",
	"Forretress",
	"Dunsparce",
	"Gligar",
	"Steelix",
	"Snubbull",
	"Granbull",
	"Qwilfish",
	"Scizor",
	"Shuckle",
	"Heracross",
	"Sneasel",
	"Teddiursa",
	"Ursaring",
	"Slugma",
	"Magcargo",
	"Swinub",
	"Piloswine",
	"Corsola",
	"Remoraid",
	"Octillery",
	"Delibird",
	"Mantine",
	"Skarmory",
	"Houndour",
	"Houndoom",
	"Kingdra",
	"Phanpy",
	"Donphan",
	"Porygon2",
	"Stantler",
	"Smeargle",
	"Tyrogue",
	"Hitmontop",
	"Smoochum",
	"Elekid",
	"Magby",
	"Miltank",
	"Blissey",
	"Raikou",
	"Entei",
	"Suicune",
	"Larvitar",
	"Pupitar",
	"Tyranitar",
	"Lugia",
	"Ho-Oh",
	"Celebi",
	"Treecko",
	"Grovyle",
	"Sceptile",
	"Torchic",
	"Combusken",
	"Blaziken",
	"Mudkip",
	"Marshtomp",
	"Swampert",
	"Poochyena",
	"Mightyena",
	"Zigzagoon",
	"Linoone",
	"Wurmple",
	"Silcoon",
	"Beautifly",
	"Cascoon",
	"Dustox",
	"Lotad",
	"Lombre",
	"Ludicolo",
	"Seedot",
	"Nuzleaf",
	"Shiftry",
	"Taillow",
	"Swellow",
	"Wingull",
	"Pelipper",
	"Ralts",
	"Kirlia",
	"Gardevoir",
	"Surskit",
	"Masquerain",
	"Shroomish",
	"Breloom",
	"Slakoth",
	"Vigoroth",
	"Slaking",
	"Nincada",
	"Ninjask",
	"Shedinja",
	"Whismur",
	"Loudred",
	"Exploud",
	"Makuhita",
	"Hariyama",
	"Azurill",
	"Nosepass",
	"Skitty",
	"Delcatty",
	"Sableye",
	"Mawile",
	"Aron",
	"Lairon",
	"Aggron",
	"Meditite",
	"Medicham",
	"Electrike",
	"Manectric",
	"Plusle",
	"Minun",
	"Volbeat",
	"Illumise",
	"Roselia",
	"Gulpin",
	"Swalot",
	"Carvanha",
	"Sharpedo",
	"Wailmer",
	"Wailord",
	"Numel",
	"Camerupt",
	"Torkoal",
	"Spoink",
	"Grumpig",
	"Spinda",
	"Trapinch",
	"Vibrava",
	"Flygon",
	"Cacnea",
	"Cacturne",
	"Swablu",
	"Altaria",
	"Zangoose",
	"Seviper",
	"Lunatone",
	"Solrock",
	"Barboach",
	"Whiscash",
	"Corphish",
	"Crawdaunt",
	"Baltoy",
	"Claydol",
	"Lileep",
	"Cradily",
	"Anorith",
	"Armaldo",
	"Feebas",
	"Milotic",
	"Castform",
	"Kecleon",
	"Shuppet",
	"Banette",
	"Duskull",
	"Dusclops",
	"Tropius",
	"Chimecho",
	"Absol",
	"Wynaut",
	"Snorunt",
	"Glalie",
	"Spheal",
	"Sealeo",
	"Walrein",
	"Clamperl",
	"Huntail",
	"Gorebyss",
	"Relicanth",
	"Luvdisc",
	"Bagon",
	"Shelgon",
	"Salamence",
	"Beldum",
	"Metang",
	"Metagross",
	"Regirock",
	"Regice",
	"Registeel",
	"Latias",
	"Latios",
	"Kyogre",
	"Groudon",
	"Rayquaza",
	"Jirachi",
	"Deoxys",
	"Turtwig",
	"Grotle",
	"Torterra",
	"Chimchar",
	"Monferno",
	"Infernape",
	"Piplup",
	"Prinplup",
	"Empoleon",
	"Starly",
	"Staravia",
	"Staraptor",
	"Bidoof",
	"Bibarel",
	"Kricketot",
	"Kricketune",
	"Shinx",
	"Luxio",
	"Luxray",
	"Budew",
	"Roserade",
	"Cranidos",
	"Rampardos",
	"Shieldon",
	"Bastiodon",
	"Burmy",
	"Wormadam",
	"Mothim",
	"Combee",
	"Vespiquen",
	"Pachirisu",
	"Buizel",
	"Floatzel",
	"Cherubi",
	"Cherrim",
	"Shellos",
	"Gastrodon",
	"Ambipom",
	"Drifloon",
	"Drifblim",
	"Buneary",
	"Lopunny",
	"Mismagius",
	"Honchkrow",
	"Glameow",
	"Purugly",
	"Chingling",
	"Stunky",
	"Skuntank",
	"Bronzor",
	"Bronzong",
	"Bonsly",
	"Mime Jr.",
	"Happiny",
	"Chatot",
	"Spiritomb",
	"Gible",
	"Gabite",
	"Garchomp",
	"Munchlax",
	"Riolu",
	"Lucario",
	"Hippopotas",
	"Hippowdon",
	"Skorupi",
	"Drapion",
	"Croagunk",
	"Toxicroak",
	"Carnivine",
	"Finneon",
	"Lumineon",
	"Mantyke",
	"Snover",
	"Abomasnow",
	"Weavile",
	"Magnezone",
	"Lickilicky",
	"Rhyperior",
	"Tangrowth",
	"Electivire",
	"Magmortar",
	"Togekiss",
	"Yanmega",
	"Leafeon",
	"Glaceon",
	"Gliscor",
	"Mamoswine",
	"Porygon-Z",
	"Gallade",
	"Probopass",
	"Dusknoir",
	"Froslass",
	"Rotom",
	"Uxie",
	"Mesprit",
	"Azelf",
	"Dialga",
	"Palkia",
	"Heatran",
	"Regigigas",
	"Giratina",
	"Cresselia",
	"Phione",
	"Manaphy",
	"Darkrai",
	"Shaymin",
	"Arceus",
	"Victini",
	"Snivy",
	"Servine",
	"Serperior",
	"Tepig",
	"Pignite",
	"Emboar",
	"Oshawott",
	"Dewott",
	"Samurott",
	"Patrat",
	"Watchog",
	"Lillipup",
	"Herdier",
	"Stoutland",
	"Purrloin",
	"Liepard",
	"Pansage",
	"Simisage",
	"Pansear",
	"Simisear",
	"Panpour",
	"Simipour",
	"Munna",
	"Musharna",
	"Pidove",
	"Tranquill",
	"Unfezant",
	"Blitzle",
	"Zebstrika",
	"Roggenrola",
	"Boldore",
	"Gigalith",
	"Woobat",
	"Swoobat",
	"Drilbur",
	"Excadrill",
	"Audino",
	"Timburr",
	"Gurdurr",
	"Conkeldurr",
	"Tympole",
	"Palpitoad",
	"Seismitoad",
	"Throh",
	"Sawk",
	"Sewaddle",
	"Swadloon",
	"Leavanny",
	"Venipede",
	"Whirlipede",
	"Scolipede",
	"Cottonee",
	"Whimsicott",
	"Petilil",
	"Lilligant",
	"Basculin",
	"Sandile",
	"Krokorok",
	"Krookodile",
	"Darumaka",
	"Darmanitan",
	"Maractus",
	"Dwebble",
	"Crustle",
	"Scraggy",
	"Scrafty",
	"Sigilyph",
	"Yamask",
	"Cofagrigus",
	"Tirtouga",
	"Carracosta",
	"Archen",
	"Archeops",
	"Trubbish",
	"Garbodor",
	"Zorua",
	"Zoroark",
	"Minccino",
	"Cinccino",
	"Gothita",
	"Gothorita",
	"Gothitelle",
	"Solosis",
	"Duosion",
	"Reuniclus",
	"Ducklett",
	"Swanna",
	"Vanillite",
	"Vanillish",
	"Vanilluxe",
	"Deerling",
	"Sawsbuck",
	"Emolga",
	"Karrablast",
	"Escavalier",
	"Foongus",
	"Amoonguss",
	"Frillish",
	"Jellicent",
	"Alomomola",
	"Joltik",
	"Galvantula",
	"Ferroseed",
	"Ferrothorn",
	"Klink",
	"Klang",
	"Klinklang",
	"Tynamo",
	"Eelektrik",
	"Eelektross",
	"Elgyem",
	"Beheeyem",
	"Litwick",
	"Lampent",
	"Chandelure",
	"Axew",
	"Fraxure",
	"Haxorus",
	"Cubchoo",
	"Beartic",
	"Cryogonal",
	"Shelmet",
	"Accelgor",
	"Stunfisk",
	"Mienfoo",
	"Mienshao",
	"Druddigon",
	"Golett",
	"Golurk",
	"Pawniard",
	"Bisharp",
	"Bouffalant",
	"Rufflet",
	"Braviary",
	"Vullaby",
	"Mandibuzz",
	"Heatmor",
	"Durant",
	"Deino",
	"Zweilous",
	"Hydreigon",
	"Larvesta",
	"Volcarona",
	"Cobalion",
	"Terrakion",
	"Virizion",
	"Tornadus",
	"Thundurus",
	"Reshiram",
	"Zekrom",
	"Landorus",
	"Kyurem",
	"Keldeo",
	"Meloetta",
	"Genesect",
	"Chespin",
	"Quilladin",
	"Chesnaught",
	"Fennekin",
	"Braixen",
	"Delphox",
	"Froakie",
	"Frogadier",
	"Greninja",
	"Bunnelby",
	"Diggersby",
	"Fletchling",
	"Fletchinder",
	"Talonflame",
	"Scatterbug",
	"Spewpa",
	"Vivillon",
	"Litleo",
	"Pyroar",
	"Flabébé",
	"Floette",
	"Florges",
	"Skiddo",
	"Gogoat",
	"Pancham",
	"Pangoro",
	"Furfrou",
	"Espurr",
	"Meowstic",
	"Honedge",
	"Doublade",
	"Aegislash",
	"Spritzee",
	"Aromatisse",
	"Swirlix",
	"Slurpuff",
	"Inkay",
	"Malamar",
	"Binacle",
	"Barbaracle",
	"Skrelp",
	"Dragalge",
	"Clauncher",
	"Clawitzer",
	"Helioptile",
	"Heliolisk",
	"Tyrunt",
	"Tyrantrum",
	"Amaura",
	"Aurorus",
	"Sylveon",
	"Hawlucha",
	"Dedenne",
	"Carbink",
	"Goomy",
	"Sliggoo",
	"Goodra",
	"Klefki",
	"Phantump",
	"Trevenant",
	"Pumpkaboo",
	"Gourgeist",
	"Bergmite",
	"Avalugg",
	"Noibat",
	"Noivern",
	"Xerneas",
	"Yveltal",
	"Zygarde",
	"Diancie",
"Hoopa",
"Volcanion",
"Deoxys Attack",
"Deoxys Defense",
"Deoxys Speed",
"Wormadam Sand",
"Wormadam Trash",
"Shaymin Sky",
"Giratina Origin",
"Rotom Heat",
"Rotom Wash",
"Rotom Frost",
"Rotom Spin",
"Rotom Cut",
"Castform Sun",
"Castform Rain",
"Castform Snow",
"Burmy Sand",
"Burmy Trash",
"Cherrim Sun",
"Shellos East",
"Gastrodon East",
"Arceus Fighting",
"Arceus Flying",
"Arceus Poison",
"Arceus Ground",
"Arceus Rock",
"Arceus Bug",
"Arceus Ghost",
"Arceus Steel",
"Arceus Fire",
"Arceus Water",
"Arceus Grass",
"Arceus Electric",
"Arceus Psychic",
"Arceus Ice",
"Arceus Dragon",
"Arceus Dark",
"Arceus Fairy",
"Unown B",
"Unown C",
"Unown D",
"Unown E",
"Unown F",
"Unown G",
"Unown H",
"Unown I",
"Unown J",
"Unown K",
"Unown L",
"Unown M",
"Unown N",
"Unown O",
"Unown P",
"Unown Q",
"Unown R",
"Unown S",
"Unown T",
"Unown U",
"Unown V",
"Unown W",
"Unown X",
"Unown Y",
"Unown Z",
"Unown !",
"Unown ?",
"Basculin Blue",
"Darmanitan Zen",
"Deerling Summer",
"Deerling Autumn",
"Deerling Winter",
"Sawsbuck Summer",
"Sawsbuck Autumn",
"Sawsbuck Winter",
"Meloetta Pirouette",
"Genesect Douse",
"Genesect Shock",
"Genesect Burn",
"Genesect Chill",
"Mii",
"Mega Venusaur",
"Mega Charizard X",
"Mega Charizard Y",
"Mega Blastoise",
"Mega Beedrill",
"Mega Pidgeot",
"Mega Alakazam",
"Mega Slowbro",
"Mega Gengar",
"Mega Kangaskhan",
"Mega Pinsir",
"Mega Gyarados",
"Mega Aerodactyl",
"Mega Mewtwo X",
"Mega Mewtwo Y",
"Mega Ampharos",
"Mega Steelix",
"Mega Scizor",
"Mega Heracross",
"Mega Houndoom",
"Mega Tyranitar",
"Mega Sceptile",
"Mega Blaziken",
"Mega Swampert",
"Mega Gardevoir",
"Mega Sableye",
"Mega Mawile",
"Mega Aggron",
"Mega Medicham",
"Mega Manectric",
"Mega Sharpedo",
"Mega Camerupt",
"Mega Altaria",
"Mega Banette",
"Mega Absol",
"Mega Glalie",
"Mega Salamence",
"Mega Metagross",
"Mega Latias",
"Mega Latios",
"Primal Kyogre",
"Primal Groudon",
"Mega Rayquaza",
"Mega Lopunny",
"Mega Garchomp",
"Mega Lucario",
"Mega Abomasnow",
"Mega Gallade",
"Mega Audino",
"Tornadus Therian",
"Thundurus Therian",
"Landorus Therian",
"Kyurem White",
"Kyurem Black",
"Keldeo Resolute",
"Vivillon Polar",
"Vivillon Tundra",
"Vivillon Continental",
"Vivillon Garden",
"Vivillon Elegant",
"Vivillon Meadow",
"Vivillon Modern",
"Vivillon Marine",
"Vivillon Archipelago",
"Vivillon High Plains",
"Vivillon Sandstorm",
"Vivillon River",
"Vivillon Monsoon",
"Vivillon Savanna",
"Vivillon Sun",
"Vivillon Ocean",
"Vivillon Jungle",
"Vivillon Fancy",
"Vivillon Poké Ball",
"Flabebe Yellow",
"Flabebe Orange",
"Flabebe Blue",
"Flabebe White",
"Floette Yellow",
"Floette Orange",
"Floette Blue",
"Floette White",
"Floette Az",
"Florges Yellow",
"Florges Orange",
"Florges Blue",
"Florges White",
"Furfrou Heart",
"Furfrou Star",
"Furfrou Diamond",
"Furfrou Debutante",
"Furfrou Matron",
"Furfrou Dandy",
"Furfrou La Reine",
"Furfrou Kabuki",
"Furfrou Pharaoh",
"Meowstic Female",
"Aegislash Blade",
"Pumpkaboo Small",
"Pumpkaboo Big",
"Pumpkaboo Super",
"Gourgeist Small",
"Gourgeist Big",
"Gourgeist Super",
"Xerneas Active",
"Mega Diancie",
"Hoopa Unbound"};

        private int[] specvals =
        {
            0x0000, // None
            0x0002, // Bulbasaur
            0x0004, // Ivysaur
            0x0006, // Venusaur
            0x0008, // Charmander
            0x000A, // Charmeleon
            0x000C, // Charizard
            0x000E, // Squirtle
            0x0010, // Wartortle
            0x0012, // Blastoise
            0x0014, // Caterpie
            0x0016, // Metapod
            0x0018, // Butterfree
            0x001A, // Weedle
            0x001C, // Kakuna
            0x001E, // Beedrill
            0x0020, // Pidgey
            0x0022, // Pidgeotto
            0x0024, // Pidgeot
            0x0026, // Rattata
            0x0028, // Raticate
            0x002A, // Spearow
            0x002C, // Fearow
            0x002E, // Ekans
            0x0030, // Arbok
            0x0032, // Pikachu
            0x0034, // Raichu
            0x0036, // Sandshrew
            0x0038, // Sandslash
            0x003A, // Nidoran♀
            0x003C, // Nidorina
            0x003E, // Nidoqueen
            0x0040, // Nidoran♂
            0x0042, // Nidorino
            0x0044, // Nidoking
            0x0046, // Clefairy
            0x0048, // Clefable
            0x004A, // Vulpix
            0x004C, // Ninetales
            0x004E, // Jigglypuff
            0x0050, // Wigglytuff
            0x0052, // Zubat
            0x0054, // Golbat
            0x0056, // Oddish
            0x0058, // Gloom
            0x005A, // Vileplume
            0x005C, // Paras
            0x005E, // Parasect
            0x0060, // Venonat
            0x0062, // Venomoth
            0x0064, // Diglett
            0x0066, // Dugtrio
            0x0068, // Meowth
            0x006A, // Persian
            0x006C, // Psyduck
            0x006E, // Golduck
            0x0070, // Mankey
            0x0072, // Primeape
            0x0074, // Growlithe
            0x0076, // Arcanine
            0x0078, // Poliwag
            0x007A, // Poliwhirl
            0x007C, // Poliwrath
            0x007E, // Abra
            0x0080, // Kadabra
            0x0082, // Alakazam
            0x0084, // Machop
            0x0086, // Machoke
            0x0088, // Machamp
            0x008A, // Bellsprout
            0x008C, // Weepinbell
            0x008E, // Victreebel
            0x0090, // Tentacool
            0x0092, // Tentacruel
            0x0094, // Geodude
            0x0096, // Graveler
            0x0098, // Golem
            0x009A, // Ponyta
            0x009C, // Rapidash
            0x009E, // Slowpoke
            0x00A0, // Slowbro
            0x00A2, // Magnemite
            0x00A4, // Magneton
            0x00A6, // Farfetch'd
            0x00A8, // Doduo
            0x00AA, // Dodrio
            0x00AC, // Seel
            0x00AE, // Dewgong
            0x00B0, // Grimer
            0x00B2, // Muk
            0x00B4, // Shellder
            0x00B6, // Cloyster
            0x00B8, // Gastly
            0x00BA, // Haunter
            0x00BC, // Gengar
            0x00BE, // Onix
            0x00C0, // Drowzee
            0x00C2, // Hypno
            0x00C4, // Krabby
            0x00C6, // Kingler
            0x00C8, // Voltorb
            0x00CA, // Electrode
            0x00CC, // Exeggcute
            0x00CE, // Exeggutor
            0x00D0, // Cubone
            0x00D2, // Marowak
            0x00D4, // Hitmonlee
            0x00D6, // Hitmonchan
            0x00D8, // Lickitung
            0x00DA, // Koffing
            0x00DC, // Weezing
            0x00DE, // Rhyhorn
            0x00E0, // Rhydon
            0x00E2, // Chansey
            0x00E4, // Tangela
            0x00E6, // Kangaskhan
            0x00E8, // Horsea
            0x00EA, // Seadra
            0x00EC, // Goldeen
            0x00EE, // Seaking
            0x00F0, // Staryu
            0x00F2, // Starmie
            0x00F4, // Mr. Mime
            0x00F6, // Scyther
            0x00F8, // Jynx
            0x00FA, // Electabuzz
            0x00FC, // Magmar
            0x00FE, // Pinsir
            0x0100, // Tauros
            0x0102, // Magikarp
            0x0104, // Gyarados
            0x0106, // Lapras
            0x0108, // Ditto
            0x010A, // Eevee
            0x010C, // Vaporeon
            0x010E, // Jolteon
            0x0110, // Flareon
            0x0112, // Porygon
            0x0114, // Omanyte
            0x0116, // Omastar
            0x0118, // Kabuto
            0x011A, // Kabutops
            0x011C, // Aerodactyl
            0x011E, // Snorlax
            0x0120, // Articuno
            0x0122, // Zapdos
            0x0124, // Moltres
            0x0126, // Dratini
            0x0128, // Dragonair
            0x012A, // Dragonite
            0x012C, // Mewtwo
            0x012E, // Mew
            0x0130, // Chikorita
            0x0132, // Bayleef
            0x0134, // Meganium
            0x0136, // Cyndaquil
            0x0138, // Quilava
            0x013A, // Typhlosion
            0x013C, // Totodile
            0x013E, // Croconaw
            0x0140, // Feraligatr
            0x0142, // Sentret
            0x0144, // Furret
            0x0146, // Hoothoot
            0x0148, // Noctowl
            0x014A, // Ledyba
            0x014C, // Ledian
            0x014E, // Spinarak
            0x0150, // Ariados
            0x0152, // Crobat
            0x0154, // Chinchou
            0x0156, // Lanturn
            0x0158, // Pichu
            0x015A, // Cleffa
            0x015C, // Igglybuff
            0x015E, // Togepi
            0x0160, // Togetic
            0x0162, // Natu
            0x0164, // Xatu
            0x0166, // Mareep
            0x0168, // Flaaffy
            0x016A, // Ampharos
            0x016C, // Bellossom
            0x016E, // Marill
            0x0170, // Azumarill
            0x0172, // Sudowoodo
            0x0174, // Politoed
            0x0176, // Hoppip
            0x0178, // Skiploom
            0x017A, // Jumpluff
            0x017C, // Aipom
            0x017E, // Sunkern
            0x0180, // Sunflora
            0x0182, // Yanma
            0x0184, // Wooper
            0x0186, // Quagsire
            0x0188, // Espeon
            0x018A, // Umbreon
            0x018C, // Murkrow
            0x018E, // Slowking
            0x0190, // Misdreavus
            0x0192, // Unown
            0x0194, // Wobbuffet
            0x0196, // Girafarig
            0x0198, // Pineco
            0x019A, // Forretress
            0x019C, // Dunsparce
            0x019E, // Gligar
            0x01A0, // Steelix
            0x01A2, // Snubbull
            0x01A4, // Granbull
            0x01A6, // Qwilfish
            0x01A8, // Scizor
            0x01AA, // Shuckle
            0x01AC, // Heracross
            0x01AE, // Sneasel
            0x01B0, // Teddiursa
            0x01B2, // Ursaring
            0x01B4, // Slugma
            0x01B6, // Magcargo
            0x01B8, // Swinub
            0x01BA, // Piloswine
            0x01BC, // Corsola
            0x01BE, // Remoraid
            0x01C0, // Octillery
            0x01C2, // Delibird
            0x01C4, // Mantine
            0x01C6, // Skarmory
            0x01C8, // Houndour
            0x01CA, // Houndoom
            0x01CC, // Kingdra
            0x01CE, // Phanpy
            0x01D0, // Donphan
            0x01D2, // Porygon2
            0x01D4, // Stantler
            0x01D6, // Smeargle
            0x01D8, // Tyrogue
            0x01DA, // Hitmontop
            0x01DC, // Smoochum
            0x01DE, // Elekid
            0x01E0, // Magby
            0x01E2, // Miltank
            0x01E4, // Blissey
            0x01E6, // Raikou
            0x01E8, // Entei
            0x01EA, // Suicune
            0x01EC, // Larvitar
            0x01EE, // Pupitar
            0x01F0, // Tyranitar
            0x01F2, // Lugia
            0x01F4, // Ho-Oh
            0x01F6, // Celebi
            0x01F8, // Treecko
            0x01FA, // Grovyle
            0x01FC, // Sceptile
            0x01FE, // Torchic
            0x0200, // Combusken
            0x0202, // Blaziken
            0x0204, // Mudkip
            0x0206, // Marshtomp
            0x0208, // Swampert
            0x020A, // Poochyena
            0x020C, // Mightyena
            0x020E, // Zigzagoon
            0x0210, // Linoone
            0x0212, // Wurmple
            0x0214, // Silcoon
            0x0216, // Beautifly
            0x0218, // Cascoon
            0x021A, // Dustox
            0x021C, // Lotad
            0x021E, // Lombre
            0x0220, // Ludicolo
            0x0222, // Seedot
            0x0224, // Nuzleaf
            0x0226, // Shiftry
            0x0228, // Taillow
            0x022A, // Swellow
            0x022C, // Wingull
            0x022E, // Pelipper
            0x0230, // Ralts
            0x0232, // Kirlia
            0x0234, // Gardevoir
            0x0236, // Surskit
            0x0238, // Masquerain
            0x023A, // Shroomish
            0x023C, // Breloom
            0x023E, // Slakoth
            0x0240, // Vigoroth
            0x0242, // Slaking
            0x0244, // Nincada
            0x0246, // Ninjask
            0x0248, // Shedinja
            0x024A, // Whismur
            0x024C, // Loudred
            0x024E, // Exploud
            0x0250, // Makuhita
            0x0252, // Hariyama
            0x0254, // Azurill
            0x0256, // Nosepass
            0x0258, // Skitty
            0x025A, // Delcatty
            0x025C, // Sableye
            0x025E, // Mawile
            0x0260, // Aron
            0x0262, // Lairon
            0x0264, // Aggron
            0x0266, // Meditite
            0x0268, // Medicham
            0x026A, // Electrike
            0x026C, // Manectric
            0x026E, // Plusle
            0x0270, // Minun
            0x0272, // Volbeat
            0x0274, // Illumise
            0x0276, // Roselia
            0x0278, // Gulpin
            0x027A, // Swalot
            0x027C, // Carvanha
            0x027E, // Sharpedo
            0x0280, // Wailmer
            0x0282, // Wailord
            0x0284, // Numel
            0x0286, // Camerupt
            0x0288, // Torkoal
            0x028A, // Spoink
            0x028C, // Grumpig
            0x028E, // Spinda
            0x0290, // Trapinch
            0x0292, // Vibrava
            0x0294, // Flygon
            0x0296, // Cacnea
            0x0298, // Cacturne
            0x029A, // Swablu
            0x029C, // Altaria
            0x029E, // Zangoose
            0x02A0, // Seviper
            0x02A2, // Lunatone
            0x02A4, // Solrock
            0x02A6, // Barboach
            0x02A8, // Whiscash
            0x02AA, // Corphish
            0x02AC, // Crawdaunt
            0x02AE, // Baltoy
            0x02B0, // Claydol
            0x02B2, // Lileep
            0x02B4, // Cradily
            0x02B6, // Anorith
            0x02B8, // Armaldo
            0x02BA, // Feebas
            0x02BC, // Milotic
            0x02BE, // Castform
            0x02C0, // Kecleon
            0x02C2, // Shuppet
            0x02C4, // Banette
            0x02C6, // Duskull
            0x02C8, // Dusclops
            0x02CA, // Tropius
            0x02CC, // Chimecho
            0x02CE, // Absol
            0x02D0, // Wynaut
            0x02D2, // Snorunt
            0x02D4, // Glalie
            0x02D6, // Spheal
            0x02D8, // Sealeo
            0x02DA, // Walrein
            0x02DC, // Clamperl
            0x02DE, // Huntail
            0x02E0, // Gorebyss
            0x02E2, // Relicanth
            0x02E4, // Luvdisc
            0x02E6, // Bagon
            0x02E8, // Shelgon
            0x02EA, // Salamence
            0x02EC, // Beldum
            0x02EE, // Metang
            0x02F0, // Metagross
            0x02F2, // Regirock
            0x02F4, // Regice
            0x02F6, // Registeel
            0x02F8, // Latias
            0x02FA, // Latios
            0x02FC, // Kyogre
            0x02FE, // Groudon
            0x0300, // Rayquaza
            0x0302, // Jirachi
            0x0304, // Deoxys
            0x0306, // Turtwig
            0x0308, // Grotle
            0x030A, // Torterra
            0x030C, // Chimchar
            0x030E, // Monferno
            0x0310, // Infernape
            0x0312, // Piplup
            0x0314, // Prinplup
            0x0316, // Empoleon
            0x0318, // Starly
            0x031A, // Staravia
            0x031C, // Staraptor
            0x031E, // Bidoof
            0x0320, // Bibarel
            0x0322, // Kricketot
            0x0324, // Kricketune
            0x0326, // Shinx
            0x0328, // Luxio
            0x032A, // Luxray
            0x032C, // Budew
            0x032E, // Roserade
            0x0330, // Cranidos
            0x0332, // Rampardos
            0x0334, // Shieldon
            0x0336, // Bastiodon
            0x0338, // Burmy
            0x033A, // Wormadam
            0x033C, // Mothim
            0x033E, // Combee
            0x0340, // Vespiquen
            0x0342, // Pachirisu
            0x0344, // Buizel
            0x0346, // Floatzel
            0x0348, // Cherubi
            0x034A, // Cherrim
            0x034C, // Shellos
            0x034E, // Gastrodon
            0x0350, // Ambipom
            0x0352, // Drifloon
            0x0354, // Drifblim
            0x0356, // Buneary
            0x0358, // Lopunny
            0x035A, // Mismagius
            0x035C, // Honchkrow
            0x035E, // Glameow
            0x0360, // Purugly
            0x0362, // Chingling
            0x0364, // Stunky
            0x0366, // Skuntank
            0x0368, // Bronzor
            0x036A, // Bronzong
            0x036C, // Bonsly
            0x036E, // Mime Jr.
            0x0370, // Happiny
            0x0372, // Chatot
            0x0374, // Spiritomb
            0x0376, // Gible
            0x0378, // Gabite
            0x037A, // Garchomp
            0x037C, // Munchlax
            0x037E, // Riolu
            0x0380, // Lucario
            0x0382, // Hippopotas
            0x0384, // Hippowdon
            0x0386, // Skorupi
            0x0388, // Drapion
            0x038A, // Croagunk
            0x038C, // Toxicroak
            0x038E, // Carnivine
            0x0390, // Finneon
            0x0392, // Lumineon
            0x0394, // Mantyke
            0x0396, // Snover
            0x0398, // Abomasnow
            0x039A, // Weavile
            0x039C, // Magnezone
            0x039E, // Lickilicky
            0x03A0, // Rhyperior
            0x03A2, // Tangrowth
            0x03A4, // Electivire
            0x03A6, // Magmortar
            0x03A8, // Togekiss
            0x03AA, // Yanmega
            0x03AC, // Leafeon
            0x03AE, // Glaceon
            0x03B0, // Gliscor
            0x03B2, // Mamoswine
            0x03B4, // Porygon-Z
            0x03B6, // Gallade
            0x03B8, // Probopass
            0x03BA, // Dusknoir
            0x03BC, // Froslass
            0x03BE, // Rotom
            0x03C0, // Uxie
            0x03C2, // Mesprit
            0x03C4, // Azelf
            0x03C6, // Dialga
            0x03C8, // Palkia
            0x03CA, // Heatran
            0x03CC, // Regigigas
            0x03CE, // Giratina
            0x03D0, // Cresselia
            0x03D2, // Phione
            0x03D4, // Manaphy
            0x03D6, // Darkrai
            0x03D8, // Shaymin
            0x03DA, // Arceus
            0x03DC, // Victini
            0x03DE, // Snivy
            0x03E0, // Servine
            0x03E2, // Serperior
            0x03E4, // Tepig
            0x03E6, // Pignite
            0x03E8, // Emboar
            0x03EA, // Oshawott
            0x03EC, // Dewott
            0x03EE, // Samurott
            0x03F0, // Patrat
            0x03F2, // Watchog
            0x03F4, // Lillipup
            0x03F6, // Herdier
            0x03F8, // Stoutland
            0x03FA, // Purrloin
            0x03FC, // Liepard
            0x03FE, // Pansage
            0x0400, // Simisage
            0x0402, // Pansear
            0x0404, // Simisear
            0x0406, // Panpour
            0x0408, // Simipour
            0x040A, // Munna
            0x040C, // Musharna
            0x040E, // Pidove
            0x0410, // Tranquill
            0x0412, // Unfezant
            0x0414, // Blitzle
            0x0416, // Zebstrika
            0x0418, // Roggenrola
            0x041A, // Boldore
            0x041C, // Gigalith
            0x041E, // Woobat
            0x0420, // Swoobat
            0x0422, // Drilbur
            0x0424, // Excadrill
            0x0426, // Audino
            0x0428, // Timburr
            0x042A, // Gurdurr
            0x042C, // Conkeldurr
            0x042E, // Tympole
            0x0430, // Palpitoad
            0x0432, // Seismitoad
            0x0434, // Throh
            0x0436, // Sawk
            0x0438, // Sewaddle
            0x043A, // Swadloon
            0x043C, // Leavanny
            0x043E, // Venipede
            0x0440, // Whirlipede
            0x0442, // Scolipede
            0x0444, // Cottonee
            0x0446, // Whimsicott
            0x0448, // Petilil
            0x044A, // Lilligant
            0x044C, // Basculin
            0x044E, // Sandile
            0x0450, // Krokorok
            0x0452, // Krookodile
            0x0454, // Darumaka
            0x0456, // Darmanitan
            0x0458, // Maractus
            0x045A, // Dwebble
            0x045C, // Crustle
            0x045E, // Scraggy
            0x0460, // Scrafty
            0x0462, // Sigilyph
            0x0464, // Yamask
            0x0466, // Cofagrigus
            0x0468, // Tirtouga
            0x046A, // Carracosta
            0x046C, // Archen
            0x046E, // Archeops
            0x0470, // Trubbish
            0x0472, // Garbodor
            0x0474, // Zorua
            0x0476, // Zoroark
            0x0478, // Minccino
            0x047A, // Cinccino
            0x047C, // Gothita
            0x047E, // Gothorita
            0x0480, // Gothitelle
            0x0482, // Solosis
            0x0484, // Duosion
            0x0486, // Reuniclus
            0x0488, // Ducklett
            0x048A, // Swanna
            0x048C, // Vanillite
            0x048E, // Vanillish
            0x0490, // Vanilluxe
            0x0492, // Deerling
            0x0494, // Sawsbuck
            0x0496, // Emolga
            0x0498, // Karrablast
            0x049A, // Escavalier
            0x049C, // Foongus
            0x049E, // Amoonguss
            0x04A0, // Frillish
            0x04A2, // Jellicent
            0x04A4, // Alomomola
            0x04A6, // Joltik
            0x04A8, // Galvantula
            0x04AA, // Ferroseed
            0x04AC, // Ferrothorn
            0x04AE, // Klink
            0x04B0, // Klang
            0x04B2, // Klinklang
            0x04B4, // Tynamo
            0x04B6, // Eelektrik
            0x04B8, // Eelektross
            0x04BA, // Elgyem
            0x04BC, // Beheeyem
            0x04BE, // Litwick
            0x04C0, // Lampent
            0x04C2, // Chandelure
            0x04C4, // Axew
            0x04C6, // Fraxure
            0x04C8, // Haxorus
            0x04CA, // Cubchoo
            0x04CC, // Beartic
            0x04CE, // Cryogonal
            0x04D0, // Shelmet
            0x04D2, // Accelgor
            0x04D4, // Stunfisk
            0x04D6, // Mienfoo
            0x04D8, // Mienshao
            0x04DA, // Druddigon
            0x04DC, // Golett
            0x04DE, // Golurk
            0x04E0, // Pawniard
            0x04E2, // Bisharp
            0x04E4, // Bouffalant
            0x04E6, // Rufflet
            0x04E8, // Braviary
            0x04EA, // Vullaby
            0x04EC, // Mandibuzz
            0x04EE, // Heatmor
            0x04F0, // Durant
            0x04F2, // Deino
            0x04F4, // Zweilous
            0x04F6, // Hydreigon
            0x04F8, // Larvesta
            0x04FA, // Volcarona
            0x04FC, // Cobalion
            0x04FE, // Terrakion
            0x0500, // Virizion
            0x0502, // Tornadus
            0x0504, // Thundurus
            0x0506, // Reshiram
            0x0508, // Zekrom
            0x050A, // Landorus
            0x050C, // Kyurem
            0x050E, // Keldeo
            0x0510, // Meloetta
            0x0512, // Genesect
            0x0514, // Chespin
            0x0516, // Quilladin
            0x0518, // Chesnaught
            0x051A, // Fennekin
            0x051C, // Braixen
            0x051E, // Delphox
            0x0520, // Froakie
            0x0522, // Frogadier
            0x0524, // Greninja
            0x0526, // Bunnelby
            0x0528, // Diggersby
            0x052A, // Fletchling
            0x052C, // Fletchinder
            0x052E, // Talonflame
            0x0530, // Scatterbug
            0x0532, // Spewpa
            0x0534, // Vivillon
            0x0536, // Litleo
            0x0538, // Pyroar
            0x053A, // Flabébé
            0x053C, // Floette
            0x053E, // Florges
            0x0540, // Skiddo
            0x0542, // Gogoat
            0x0544, // Pancham
            0x0546, // Pangoro
            0x0548, // Furfrou
            0x054A, // Espurr
            0x054C, // Meowstic
            0x054E, // Honedge
            0x0550, // Doublade
            0x0552, // Aegislash
            0x0554, // Spritzee
            0x0556, // Aromatisse
            0x0558, // Swirlix
            0x055A, // Slurpuff
            0x055C, // Inkay
            0x055E, // Malamar
            0x0560, // Binacle
            0x0562, // Barbaracle
            0x0564, // Skrelp
            0x0566, // Dragalge
            0x0568, // Clauncher
            0x056A, // Clawitzer
            0x056C, // Helioptile
            0x056E, // Heliolisk
            0x0570, // Tyrunt
            0x0572, // Tyrantrum
            0x0574, // Amaura
            0x0576, // Aurorus
            0x0578, // Sylveon
            0x057A, // Hawlucha
            0x057C, // Dedenne
            0x057E, // Carbink
            0x0580, // Goomy
            0x0582, // Sliggoo
            0x0584, // Goodra
            0x0586, // Klefki
            0x0588, // Phantump
            0x058A, // Trevenant
            0x058C, // Pumpkaboo
            0x058E, // Gourgeist
            0x0590, // Bergmite
            0x0592, // Avalugg
            0x0594, // Noibat
            0x0596, // Noivern
            0x0598, // Xerneas
            0x059A, // Yveltal
            0x059C, // Zygarde
            0x059E, // Diancie
            0x05A0, // Hoopa
            0x05A2, // Volcanion
            0x0B04, // Deoxys Attack
            0x1304, // Deoxys Defense
            0x1B04, // Deoxys Speed
            0x0B3A, // Wormadam Sand
            0x133A, // Wormadam Trash
            0x0BD8, // Shaymin Sky
            0x0BCE, // Giratina Origin
            0x0BBE, // Rotom Heat
            0x13BE, // Rotom Wash
            0x1BBE, // Rotom Frost
            0x23BE, // Rotom Spin
            0x2BBE, // Rotom Cut
            0x0ABE, // Castform Sun
            0x12BE, // Castform Rain
            0x1ABE, // Castform Snow
            0x0B38, // Burmy Sand
            0x1338, // Burmy Trash
            0x0B4A, // Cherrim Sun
            0x0B4C, // Shellos East
            0x0B4E, // Gastrodon East
            0x0BDA, // Arceus Fighting
            0x13DA, // Arceus Flying
            0x1BDA, // Arceus Poison
            0x23DA, // Arceus Ground
            0x2BDA, // Arceus Rock
            0x33DA, // Arceus Bug
            0x3BDA, // Arceus Ghost
            0x43DA, // Arceus Steel
            0x4BDA, // Arceus Fire
            0x53DA, // Arceus Water
            0x5BDA, // Arceus Grass
            0x63DA, // Arceus Electric
            0x6BDA, // Arceus Psychic
            0x73DA, // Arceus Ice
            0x7BDA, // Arceus Dragon
            0x83DA, // Arceus Dark
            0x8BDA, // Arceus Fairy
            0x0992, // Unown B
            0x1192, // Unown C
            0x1992, // Unown D
            0x2192, // Unown E
            0x2992, // Unown F
            0x3192, // Unown G
            0x3992, // Unown H
            0x4192, // Unown I
            0x4992, // Unown J
            0x5192, // Unown K
            0x5992, // Unown L
            0x6192, // Unown M
            0x6992, // Unown N
            0x7192, // Unown O
            0x7992, // Unown P
            0x8192, // Unown Q
            0x8992, // Unown R
            0x9192, // Unown S
            0x9992, // Unown T
            0xA192, // Unown U
            0xA992, // Unown V
            0xB192, // Unown W
            0xB992, // Unown X
            0xC192, // Unown Y
            0xC992, // Unown Z
            0xD192, // Unown !
            0xD992, // Unown ?
            0x0C4C, // Basculin Blue
            0x0C56, // Darmanitan Zen
            0x0C92, // Deerling Summer
            0x1492, // Deerling Autumn
            0x1C92, // Deerling Winter
            0x0C94, // Sawsbuck Summer
            0x1494, // Sawsbuck Autumn
            0x1C94, // Sawsbuck Winter
            0x0D10, // Meloetta Pirouette
            0x0D12, // Genesect Douse
            0x1512, // Genesect Shock
            0x1D12, // Genesect Burn
            0x2512, // Genesect Chill
            0xFFFC, // Mii
            0x0806, // Mega Venusaur
            0x080C, // Mega Charizard X
            0x100C, // Mega Charizard Y
            0x0812, // Mega Blastoise
            0x081E, // Mega Beedrill
            0x0824, // Mega Pidgeot
            0x0882, // Mega Alakazam
            0x08A0, // Mega Slowbro
            0x08BC, // Mega Gengar
            0x08E6, // Mega Kangaskhan
            0x08FE, // Mega Pinsir
            0x0904, // Mega Gyarados
            0x091C, // Mega Aerodactyl
            0x092C, // Mega Mewtwo X
            0x112C, // Mega Mewtwo Y
            0x096A, // Mega Ampharos
            0x09A0, // Mega Steelix
            0x09A8, // Mega Scizor
            0x09AC, // Mega Heracross
            0x09CA, // Mega Houndoom
            0x09F0, // Mega Tyranitar
            0x09FC, // Mega Sceptile
            0x0A02, // Mega Blaziken
            0x0A08, // Mega Swampert
            0x0A34, // Mega Gardevoir
            0x0A5C, // Mega Sableye
            0x0A5E, // Mega Mawile
            0x0A64, // Mega Aggron
            0x0A68, // Mega Medicham
            0x0A6C, // Mega Manectric
            0x0A7E, // Mega Sharpedo
            0x0A86, // Mega Camerupt
            0x0A9C, // Mega Altaria
            0x0AC4, // Mega Banette
            0x0ACE, // Mega Absol
            0x0AD4, // Mega Glalie
            0x0AEA, // Mega Salamence
            0x0AF0, // Mega Metagross
            0x0AF8, // Mega Latias
            0x0AFA, // Mega Latios
            0x0AFC, // Primal Kyogre
            0x0AFE, // Primal Groudon
            0x0B00, // Mega Rayquaza
            0x0B58, // Mega Lopunny
            0x0B7A, // Mega Garchomp
            0x0B80, // Mega Lucario
            0x0B98, // Mega Abomasnow
            0x0BB6, // Mega Gallade
            0x0C26, // Mega Audino
            0x0D02, // Tornadus Therian
            0x0D04, // Thundurus Therian
            0x0D0A, // Landorus Therian
            0x0D0C, // Kyurem White
            0x150C, // Kyurem Black
            0x0D0E, // Keldeo Resolute
            0x0D34, // Vivillon Polar
            0x1534, // Vivillon Tundra
            0x1D34, // Vivillon Continental
            0x2534, // Vivillon Garden
            0x2D34, // Vivillon Elegant
            0x3534, // Vivillon Meadow
            0x3D34, // Vivillon Modern
            0x4534, // Vivillon Marine
            0x4D34, // Vivillon Archipelago
            0x5534, // Vivillon High Plains
            0x5D34, // Vivillon Sandstorm
            0x6534, // Vivillon River
            0x6D34, // Vivillon Monsoon
            0x7534, // Vivillon Savanna
            0x7D34, // Vivillon Sun
            0x8534, // Vivillon Ocean
            0x8D34, // Vivillon Jungle
            0x9534, // Vivillon Fancy
            0x9D34, // Vivillon Poké Ball
            0x0D3A, // Flabebe Yellow
            0x153A, // Flabebe Orange
            0x1D3A, // Flabebe Blue
            0x253A, // Flabebe White
            0x0D3C, // Floette Yellow
            0x153C, // Floette Orange
            0x1D3C, // Floette Blue
            0x253C, // Floette White
            0x2D3C, // Floette Az
            0x0D3E, // Florges Yellow
            0x153E, // Florges Orange
            0x1D3E, // Florges Blue
            0x253E, // Florges White
            0x0D48, // Furfrou Heart
            0x1548, // Furfrou Star
            0x1D48, // Furfrou Diamond
            0x2548, // Furfrou Debutante
            0x2D48, // Furfrou Matron
            0x3548, // Furfrou Dandy
            0x3D48, // Furfrou La Reine
            0x4548, // Furfrou Kabuki
            0x4D48, // Furfrou Pharaoh
            0x0D4C, // Meowstic Female
            0x0D52, // Aegislash Blade
            0x0D8C, // Pumpkaboo Small
            0x158C, // Pumpkaboo Big
            0x1D8C, // Pumpkaboo Super
            0x0D8E, // Gourgeist Small
            0x158E, // Gourgeist Big
            0x1D8E, // Gourgeist Super
            0x0D98, // Xerneas Active
            0x0D9E, // Mega Diancie
            0x0DA0, // Hoopa Unbound
        };

        private void B_Open_Click(object sender, EventArgs e)
        {
            B_Go.Enabled = false;
            TB_In.Text = "";
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                TB_In.Text = ofd.FileName;
                B_Go.Enabled = true;
            }
        }

    }

    public class Move
    {
        public int id;
        public string category;
        public string accuracy;
        public string name;
        public string power;
        public string pp;
        public string type;

        public Move(string m)
        {
            string[] mm = m.Split(',');
            this.category = mm[1];
            if (mm[2] != "undefined")
            {
                Int32.TryParse(mm[2], out this.id);
            }
            else
                this.id = -1;
            this.accuracy = mm[3];
            if (this.accuracy == "true")
                this.accuracy = "null";
            this.name = mm[4];
            this.power = mm[5];
            this.pp = mm[6];
            this.type = mm[7];
        }
    }
}
