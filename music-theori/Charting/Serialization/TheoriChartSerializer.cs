using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using theori.Audio.Effects;
using theori.Charting.Effects;
using theori.GameModes;

namespace theori.Charting.Serialization
{
    /// <summary>
    /// The default chart format for all :theori game modes.
    /// A .theori file is a json object 
    /// </summary>
    public sealed class TheoriChartSerializer : IChartSerializer
    {
        public string ParentDirectory { get; }

        private readonly GameMode m_gameMode;

        public TheoriChartSerializer(string chartsDir, GameMode gameMode)
        {
            ParentDirectory = chartsDir;
            m_gameMode = gameMode;
        }

        public Chart LoadFromFile(ChartInfo chartInfo)
        {
            string chartFile = Path.Combine(ParentDirectory, chartInfo.Set.FilePath, chartInfo.FileName);

            // TODO(local): We don't need to be creating chart factories like this aaa but it's a start
            Chart chart = m_gameMode.GetChartFactory().CreateNew();
            //chart.GameMode = m_gameMode; // this is done by the factory, because it's required.
            chart.Offset = chartInfo.ChartOffset;
            chart.Info = chartInfo;

            var jobj = JObject.Load(new JsonTextReader(new StreamReader(File.OpenRead(chartFile))));
            dynamic jobjdyn = jobj;

            JArray controlPoints = jobjdyn.controlPoints;
            JArray lanes = jobjdyn.lanes;

            static HybridLabel ToLabel(JToken token)
            {
                return token.Type switch
                {
                    JTokenType.String => (string)token,
                    JTokenType.Integer => (int)token,
                    _ => throw new ChartFormatException("Invalid value for lane label."),
                };
            }

            static object ToValue(dynamic jObjectDyn, Type typeHint)
            {
                if (typeHint == typeof(bool)) return (bool)jObjectDyn;
                else if (typeHint == typeof(sbyte)) return (sbyte)jObjectDyn;
                else if (typeHint == typeof(short)) return (short)jObjectDyn;
                else if (typeHint == typeof(int)) return (int)jObjectDyn;
                else if (typeHint == typeof(long)) return (long)jObjectDyn;
                else if (typeHint == typeof(byte)) return (byte)jObjectDyn;
                else if (typeHint == typeof(ushort)) return (ushort)jObjectDyn;
                else if (typeHint == typeof(uint)) return (uint)jObjectDyn;
                else if (typeHint == typeof(ulong)) return (ulong)jObjectDyn;
                else if (typeHint == typeof(float)) return (float)jObjectDyn;
                else if (typeHint == typeof(double)) return (double)jObjectDyn;
                else if (typeHint == typeof(string)) return (string)jObjectDyn;
                else if (typeHint == typeof(char)) return (char)jObjectDyn;
                else if (typeHint == typeof(tick_t)) return (tick_t)(double)jObjectDyn;
                else if (typeHint == typeof(time_t)) return (time_t)(double)jObjectDyn;
                else if (typeHint.IsEnum) return Enum.Parse(typeHint, (string)jObjectDyn);
                // TODO(local): verify that array is a thing that works. Nothing uses it yet
                else if (typeHint.IsArray && typeHint.GetElementType() == typeof(EffectDef))
                {
                    var result = new List<EffectDef>();
                    foreach (var effect in (JArray)jObjectDyn)
                        result.Add((EffectDef)ToValue(effect, typeof(EffectDef)));
                    return result.ToArray();
                }
                else if (typeof(IEffectParam).IsAssignableFrom(typeHint))
                {
                    bool isRange = jObjectDyn is JObject;
                    if (typeHint == typeof(EffectParamI))
                    {
                        if (isRange)
                            return new EffectParamI((int)jObjectDyn.min, (int)jObjectDyn.max, (Ease)Enum.Parse(typeof(Ease), (string)jObjectDyn.ease));
                        return new EffectParamI((int)jObjectDyn);
                    }
                    else if (typeHint == typeof(EffectParamF))
                    {
                        if (isRange)
                        {
                            if (((JObject)jObjectDyn).ContainsKey("type") && (string)jObjectDyn.type == "x")
                                return new EffectParamX((int)jObjectDyn.min, (int)jObjectDyn.max);
                            return new EffectParamF((float)jObjectDyn.min, (float)jObjectDyn.max, (Ease)Enum.Parse(typeof(Ease), (string)jObjectDyn.ease));
                        }
                        return new EffectParamF((float)jObjectDyn);
                    }
                    else if (typeHint == typeof(EffectParamX))
                    {
                        if (isRange)
                            return new EffectParamX((int)jObjectDyn.min, (int)jObjectDyn.max);
                        return new EffectParamX((int)jObjectDyn);
                    }
                    else if (typeHint == typeof(EffectParamS))
                        return new EffectParamS((string)jObjectDyn);
                    return null;
                }
                else
                {
                    var obj = Activator.CreateInstance(typeHint);
                    var jObject = (JObject)jObjectDyn;

                    foreach (var prop in obj.GetTheoriPropertyInfos())
                    {
                        string name = prop.GetAttribute<TheoriPropertyAttribute>()?.OverrideName ?? prop.Name;
                        if (!jObject.ContainsKey(name)) continue;

                        if (prop.Type == typeof(EffectDef) || prop.Type.IsSubclassOf(typeof(EffectDef)))
                            prop.Value = ToEffectDef(jObject.GetValue(name));
                        else prop.Value = ToValue(jObject.GetValue(name), prop.Type);
                    }

                    return obj;
                }

            }

            static Entity ToEntity(dynamic entityObj)
            {
                string entityId = (string)entityObj.type;
                return (Entity)ToValue(entityObj, Entity.GetEntityTypeById(entityId));
            }

            static EffectDef ToEffectDef(dynamic effectObj)
            {
                string effectId = (string)effectObj.type;
                return (EffectDef)ToValue(effectObj, EffectDef.GetEntityTypeById(effectId));
            }

            static tick_t ToTickT(JToken token) => (double)token;

            foreach (dynamic pointObj in controlPoints)
            {
                tick_t pos = ToTickT(pointObj.position);
                var point = chart.ControlPoints.GetOrCreate(pos, true);
                point.BeatCount = (int)pointObj.numerator;
                point.BeatKind = (int)pointObj.denominator;
                point.SpeedMultiplier = (double)pointObj.multiplier;
                point.BeatsPerMinute = (double)pointObj.bpm;
                if (((JObject)pointObj).ContainsKey("stop"))
                    point.StopChart = (bool)pointObj.stop;
            }

            foreach (dynamic laneObj in lanes)
            {
                var lane = chart[ToLabel(laneObj.label)];

                foreach (dynamic entityObj in laneObj.entities)
                {
                    var entity = ToEntity(entityObj);
                    lane.Add(entity);
                }
            }

            return chart;
        }

        public void SaveToFile(Chart chart)
        {
            var chartInfo = chart.Info;
            string chartFile = Path.Combine(ParentDirectory, chartInfo.Set.FilePath, chartInfo.FileName);

            var stringWriter = new StringWriter();
            using (var writer = ChartWriter.ToString(stringWriter))
            {
                writer.WriteStartStructure();
                {
                    writer.WritePropertyName("controlPoints");
                    writer.WriteStartArray();
                    {
                        for (int i = 0; i < chart.ControlPoints.Count; i++)
                        {
                            var cp = chart.ControlPoints[i];
                            if (cp == null)
                                Logger.Log($"Null object in control points at { i }");
                            else writer.WriteValue(cp);
                        }
                    }
                    writer.WriteEndArray();

                    writer.WritePropertyName("lanes");
                    writer.WriteStartArray();
                    {
                        foreach (var lane in chart.Lanes)
                        {
                            writer.WriteStartStructure();

                            writer.WritePropertyName("label");
                            writer.WriteValue(lane.Label);

                            writer.WritePropertyName("entities");
                            writer.WriteStartArray();
                            for (int i = 0; i < lane.Count; i++)
                            {
                                var obj = lane[i];
                                if (obj == null)
                                    Logger.Log($"Null object in stream { lane.Label } at { i }");
                                else writer.WriteValue(obj);
                            }
                            writer.WriteEndArray();

                            writer.WriteEndStructure();
                        }
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndStructure();

                writer.Flush();
                string result = stringWriter.ToString();

                Directory.CreateDirectory(Directory.GetParent(chartFile).FullName);
                File.WriteAllText(chartFile, result.FormatJson());
            }
        }
    }
}
