using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class PropickReading
    {
        [ProtoMember(1)]
        public Vec3d Position = new Vec3d();
        [ProtoMember(2)]
        public Dictionary<string, OreReading> OreReadings = new Dictionary<string, OreReading>();
        [ProtoMember(3)]
        public string Guid { get; set; }

        public double HighestReading
        {
            get
            {
                double maxreading = 0;
                foreach (var val in OreReadings)
                {
                    maxreading = GameMath.Max(maxreading, val.Value.TotalFactor);
                }
                return maxreading;
            }
        }

        public static double MentionThreshold = 0.002;

        public string ToHumanReadable(string languageCode, Dictionary<string, string> pageCodes)
        {
            List<KeyValuePair<double, string>> readouts = new List<KeyValuePair<double, string>>();
            List<string> traceamounts = new List<string>();

            foreach (var val in OreReadings)
            {
                var reading = val.Value;
                if (reading.DepositCode == "unknown")
                {
                    string text = Lang.GetL(languageCode, "propick-reading-unknown", val.Key);
                    readouts.Add(new KeyValuePair<double, string>(1, text));
                    continue;
                }

                string[] names = new string[] { "propick-density-verypoor", "propick-density-poor", "propick-density-decent", "propick-density-high", "propick-density-veryhigh", "propick-density-ultrahigh" };

                if (reading.TotalFactor > 0.025)
                {
                    string pageCode = pageCodes[val.Key];
                    string text = Lang.GetL(languageCode, "propick-reading", Lang.GetL(languageCode, names[(int)GameMath.Clamp(reading.TotalFactor * 7.5f, 0, 5)]), pageCode, Lang.GetL(languageCode, "ore-" + val.Key), reading.PartsPerThousand.ToString("0.##"));
                    readouts.Add(new KeyValuePair<double, string>(reading.TotalFactor, text));
                }
                else if (reading.TotalFactor > MentionThreshold)
                {
                    traceamounts.Add(val.Key);
                }
            }

            StringBuilder sb = new StringBuilder();

            if (readouts.Count >= 0 || traceamounts.Count > 0)
            {
                var elems = readouts.OrderByDescending(val => val.Key);

                sb.AppendLine(Lang.GetL(languageCode, "propick-reading-title", readouts.Count));
                foreach (var elem in elems) sb.AppendLine(elem.Value);

                if (traceamounts.Count > 0)
                {
                    var sbTrace = new StringBuilder();
                    int i = 0;
                    foreach (var val in traceamounts)
                    {
                        if (i > 0) sbTrace.Append(", ");
                        string pageCode = pageCodes[val];
                        string text = string.Format("<a href=\"handbook://{0}\">{1}</a>", pageCode, Lang.GetL(languageCode, "ore-" + val));
                        sbTrace.Append(text);
                        i++;
                    }

                    sb.Append(Lang.GetL(languageCode, "Miniscule amounts of {0}", sbTrace.ToString()));
                    sb.AppendLine();
                }
            }
            else
            {
                sb.Append(Lang.GetL(languageCode, "propick-noreading"));
            }

            return sb.ToString();
        }

        internal double GetTotalFactor(string orecode)
        {
            if (!OreReadings.TryGetValue(orecode, out var reading))
            {
                return 0;
            }
            return reading.TotalFactor;
        }
    }
}
