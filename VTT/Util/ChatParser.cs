namespace VTT.Util
{
    using NCalc;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using VTT.Control;
    using VTT.Network;

    public static class ChatParser
    {
        public static ChatLine Parse(string text, Color userColor, string username)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (text.Contains("%s")) // Escape ImGui (c++) formatting symbol
            {
                return null;
            }

            if (text.StartsWith('/')) // Special commands
            {
                if (text.StartsWith("/r ") || text.StartsWith("/roll "))
                {
                    text = "[m:RollExpression]" + SplitXdY(text[text.IndexOf(' ')..]);
                }

                if (text.StartsWith("/w ") || text.StartsWith("/whisper "))
                {
                    int sIndex = text.IndexOf(' ');
                    int sIndex2 = text.IndexOf(' ', sIndex + 1);
                    if (sIndex != -1 && sIndex2 != -1)
                    {
                        text = $"[d:{text[(sIndex + 1)..sIndex2]}]{text[(sIndex2 + 1)..]}";
                    }
                }

                if (text.StartsWith("/gr ") || text.StartsWith("/gmroll ") || text.StartsWith("/gm roll "))
                {
                    Guid adminID = Server.Instance.GetAnyAdmin();
                    text = $"[d:{adminID}][m:RollExpression]" + SplitXdY(text[(text.StartsWith("/gm roll ") ? text.IndexOf(' ', 4) : text.IndexOf(' '))..]);
                }

                if (text.Equals("/session start"))
                {
                    string time = DateTime.Now.ToLocalTime().ToString("dd/MM/yyyy HH:mm \"GMT\"zzz");
                    text = $"[m:SessionMarker][p:Session Start][p:{time}]";
                }

                if (text.Equals("/session end"))
                {
                    string time = DateTime.Now.ToLocalTime().ToString("dd/MM/yyyy HH:mm \"GMT\"zzz");
                    text = $"[m:SessionMarker][p:Session End][p:{time}]";
                }
            }

            int idx = 0;
            List<ChatBlock> blocks = new List<ChatBlock>();
            Color c = Extensions.FromAbgr(0);
            Color dc = Extensions.FromAbgr(0);
            string tt = string.Empty;
            Guid destID = Guid.Empty;
            Guid pId = Guid.Empty;
            string displayName = username;
            string destname = string.Empty;
            ChatLine.RenderType renderType = ChatLine.RenderType.Line;
            while (ParseBlock(text, userColor, ref c, ref dc, ref tt, ref idx, ref displayName, ref destname, ref destID, ref pId, ref renderType, out ChatBlock cb))
            {
                if (cb != null)
                {
                    blocks.Add(cb);
                }
            }

            ChatLine ret = new ChatLine() { Blocks = blocks, Sender = username, SenderDisplayName = displayName, DestDisplayName = destname, DestID = destID, PortraitID = pId, SenderColor = userColor, DestColor = dc, Type = renderType };
            return ret;
        }

        private static readonly Regex regexXdY = new Regex("[0-9]+d[0-9]+", RegexOptions.Multiline | RegexOptions.Compiled);
        public static string FixXdY(string input)
        {
            return regexXdY.Replace(input, s =>
            {
                if (s.Success)
                {
                    int indexD = s.Value.IndexOf('d');
                    string l = s.Value[..indexD];
                    string r = s.Value[(indexD + 1)..];
                    if (int.TryParse(l, out int nDice) && int.TryParse(r, out int dieSide))
                    {
                        return $"roll({nDice}, {dieSide})";
                    }
                }

                return s.Value;
            });
        }

        public static string SplitXdY(string input)
        {
            return regexXdY.Replace(input, s =>
            {
                if (s.Success)
                {
                    int indexD = s.Value.IndexOf('d');
                    string l = s.Value[..indexD];
                    string r = s.Value[(indexD + 1)..];
                    if (int.TryParse(l, out int nDice) && int.TryParse(r, out int dieSide))
                    {
                        string ret = "(";
                        for (int i = 0; i < nDice; ++i)
                        {
                            ret += $"[roll(1, {dieSide})]";
                            if (i != nDice - 1)
                            {
                                ret += " + ";
                            }
                        }

                        return ret + ")";
                    }
                }

                return s.Value;
            });
        }

        public static bool TryParseTextAsExpression(string exp, bool autoCorrextXdY, out double result)
        {
            if (autoCorrextXdY)
            {
                exp = FixXdY(exp);
            }

            if (TryRunExpression(exp, out object res, out _))
            {
                result = Convert.ToDouble(res);
                return true;
            }
            else
            {
                result = double.NaN;
                return false;
            }
        }

        public static ChatBlock ParseExpression(string exp)
        {
            if (TryRunExpression(exp, out object result, out RollExpressionEvaluator evaluator))
            {
                Color rollColor = evaluator.ResultFlags switch
                {
                    RollResultFlags.HadMaximumAndMinimumValue => Color.LightBlue,
                    RollResultFlags.HadMaximumValue => Color.LightGreen,
                    RollResultFlags.HadMinimumValue => Color.Red,
                    _ => Color.White
                };

                PrepareRollsTooltip(ref exp, evaluator.RollResults);
                return new ChatBlock() { Color = rollColor, Text = result.ToString(), Tooltip = $"{exp} = {result}", Type = ChatBlockType.Expression, RollContents = GetRollContents(evaluator.RollsMade) };
            }
            else
            {
                return new ChatBlock() { Color = Color.Red, Text = exp, Tooltip = "An exception occured while evaluating", Type = ChatBlockType.ExpressionError, RollContents = ChatBlockExpressionRollContents.None };
            }
        }

        public static bool ParseBlock(string text, Color userColor, ref Color color, ref Color descColor, ref string tooltip, ref int idx, ref string username, ref string destname, ref Guid destID, ref Guid portraitID, ref ChatLine.RenderType renderType, out ChatBlock cb)
        {
            if (idx >= text.Length)
            {
                cb = null;
                return false;
            }

            StringBuilder sb = new StringBuilder();
            bool isBlock = false;
            BlockMode blockMode = BlockMode.Text;
            int brackets = 0;
            while (true)
            {
                if (!MoveNext(text, ref idx, out char c))
                {
                    break;
                }

                if (c == '[' && !IsEscaped(text, idx - 1))
                {
                    if (!isBlock)
                    {
                        if (sb.Length == 0)
                        {
                            isBlock = true;
                            if (!MoveNext(text, ref idx, out c))
                            {
                                break;
                            }

                            if (c == 'c') // Color
                            {
                                if (!MoveNext(text, ref idx, out c) || !MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.ColorSpecifier;
                                if (c == 'u')
                                {
                                    color = userColor;
                                    blockMode = BlockMode.Skip;
                                    continue;
                                }

                                if (c == 'r')
                                {
                                    color = Extensions.FromAbgr(0);
                                    blockMode = BlockMode.Skip;
                                    continue;
                                }

                                --idx;
                                continue;
                            }

                            if (c == 't') // Tooltip
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.TooltipSpecifier;
                                continue;
                            }

                            if (c == 'm') // Render mode specifier
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.RenderModeSpecifier;
                                continue;
                            }

                            if (c == 'p') // Passthrough block
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.Passthrough;
                                continue;
                            }

                            if (c == 'r') // Recursive collections
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                if (c == ':')
                                {
                                    blockMode = BlockMode.Recursive;
                                    continue;
                                }
                                else
                                {
                                    idx -= 1;
                                    c = text[idx];
                                    goto expr;
                                }
                            }

                            if (c == 'd') // Destination
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.DestinationSpecifier;
                                continue;
                            }

                            if (c == 'n') // Name
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.SenderNameSpecifier;
                                continue;
                            }

                            if (c == 'o') // Object ref
                            {
                                if (!MoveNext(text, ref idx, out c))
                                {
                                    blockMode = BlockMode.Error;
                                    break;
                                }

                                blockMode = BlockMode.SenderPortraitSpecifier;
                                continue;
                            }

                        expr:
                            --idx;
                            // Expression
                            blockMode = BlockMode.Expression;
                            continue;
                        }
                        else
                        {
                            idx--;
                            break;
                        }
                    }
                    else
                    {
                        if (blockMode == BlockMode.Recursive)
                        {
                            brackets += 1;
                        }
                        else
                        {
                            idx--;
                            break;
                        }
                    }
                }

                if (c == ']' && !IsEscaped(text, idx - 1) && isBlock)
                {
                    if (brackets == 0)
                    {
                        break;
                    }

                    brackets -= 1;
                }

                if (c != '\\' || IsEscaped(text, idx - 1))
                {
                    sb.Append(c);
                }
            }

            if (blockMode == BlockMode.Text)
            {
                string bContent = sb.ToString();
                if (!string.IsNullOrEmpty(bContent))
                {
                    cb = new ChatBlock() { Color = color, Text = bContent, Tooltip = tooltip, Type = ChatBlockType.Text, RollContents = ChatBlockExpressionRollContents.None };
                    tooltip = string.Empty;
                }
                else
                {
                    cb = null;
                }

                return true;
            }

            if (blockMode == BlockMode.Error)
            {
                cb = new ChatBlock() { Color = Color.Red, Text = sb.ToString(), Tooltip = "An exception occured while parsing this text!", Type = ChatBlockType.TextError, RollContents = ChatBlockExpressionRollContents.None };
                return true;
            }

            if (blockMode == BlockMode.ColorSpecifier)
            {
                string bContent = sb.ToString();
                if (bContent.StartsWith("0x"))
                {
                    bContent = bContent[2..];
                }

                if (uint.TryParse(bContent, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out uint i))
                {
                    color = Extensions.FromArgb(i);
                }

                cb = null;
                return true;
            }

            if (blockMode == BlockMode.Skip)
            {
                cb = null;
                return true;
            }

            if (blockMode == BlockMode.TooltipSpecifier)
            {
                tooltip = sb.ToString();
                cb = null;
                return true;
            }

            if (blockMode == BlockMode.RenderModeSpecifier)
            {
                if (Enum.TryParse(sb.ToString(), out ChatLine.RenderType result))
                {
                    renderType = result;
                }

                cb = null;
                return true;
            }

            if (blockMode == BlockMode.Expression)
            {
                string bContent = sb.ToString();
                if (TryRunExpression(bContent, out object result, out RollExpressionEvaluator evaluator))
                {
                    Color rollColor = evaluator.ResultFlags switch
                    {
                        RollResultFlags.HadMaximumAndMinimumValue => Color.LightBlue,
                        RollResultFlags.HadMaximumValue => Color.LightGreen,
                        RollResultFlags.HadMinimumValue => Color.Red,
                        _ => Color.White
                    };

                    PrepareRollsTooltip(ref bContent, evaluator.RollResults);
                    cb = new ChatBlock() { Color = rollColor, Text = result.ToString(), Tooltip = $"{bContent} = {result}", Type = ChatBlockType.Expression, RollContents = GetRollContents(evaluator.RollsMade) };
                    return true;
                }
                else
                {
                    cb = new ChatBlock() { Color = Color.Red, Text = bContent, Tooltip = "An exception occured while evaluating", Type = ChatBlockType.ExpressionError, RollContents = ChatBlockExpressionRollContents.None };
                    return true;
                }
            }

            if (blockMode == BlockMode.Passthrough)
            {
                cb = new ChatBlock() { Color = color, Text = sb.ToString(), Tooltip = tooltip, Type = ChatBlockType.Text, RollContents = ChatBlockExpressionRollContents.None };
                return true;
            }

            if (blockMode == BlockMode.Recursive)
            {
                string t = sb.ToString();

                int rIdx = 0;
                List<ChatBlock> blocks = new List<ChatBlock>();
                Color c = color;
                string tt = string.Empty;
                ChatLine.RenderType rRenderType = ChatLine.RenderType.Line;
                string rline = string.Empty;
                ChatBlockExpressionRollContents compoundContents = ChatBlockExpressionRollContents.None;

                while (ParseBlock(t, userColor, ref c, ref descColor, ref tt, ref rIdx, ref username, ref destname, ref destID, ref portraitID, ref rRenderType, out ChatBlock rCb))
                {
                    if (rCb != null)
                    {
                        rline += rCb.Text;
                        compoundContents |= rCb.RollContents;
                    }
                }

                cb = new ChatBlock() { Color = color, Text = rline, Tooltip = sb.ToString(), Type = ChatBlockType.Compound, RollContents = compoundContents };
                return true;
            }

            if (blockMode == BlockMode.DestinationSpecifier)
            {
                string t = sb.ToString();
                Guid id = Guid.Empty;
                if (!Guid.TryParse(t, out id))
                {
                    if (t.ToLower().Equals("gm"))
                    {
                        id = Server.Instance.GetAnyAdmin();
                    }
                    else
                    {
                        foreach (ClientInfo ci in Server.Instance.ClientInfos.Values)
                        {
                            if (ci.Name.ToLower().Equals(t.ToLower()))
                            {
                                id = ci.ID;
                                break;
                            }
                        }
                    }
                }

                if (!id.Equals(Guid.Empty))
                {
                    foreach (ClientInfo ci in Server.Instance.ClientInfos.Values)
                    {
                        if (ci.ID.Equals(id))
                        {
                            destname = ci.Name;
                            descColor = ci.Color;
                            break;
                        }
                    }
                }
                else
                {
                    destname = string.Empty;
                    descColor = Extensions.FromAbgr(0);
                }

                destID = id;
                cb = null;
                return true;
            }

            if (blockMode == BlockMode.SenderNameSpecifier)
            {
                string t = sb.ToString();
                if (!string.IsNullOrEmpty(t))
                {
                    username = t;
                }

                cb = null;
                return true;
            }

            if (blockMode == BlockMode.SenderPortraitSpecifier)
            {
                string t = sb.ToString();
                if (Guid.TryParse(t, out Guid id))
                {
                    portraitID = id;
                }

                cb = null;
                return true;
            }

            cb = null;
            return false;
        }

        public static bool TryRunExpression(string expr, out object result, out RollExpressionEvaluator evaluator)
        {
            // The reason statics switch to instanced here is to avoid race conditions on evaluations from multiple threads
            evaluator = new RollExpressionEvaluator(expr);
            bool ret = evaluator.TryRun(out result);
            return ret;
        }

        private static void PrepareRollsTooltip(ref string expr, Dictionary<string, List<string>> rollResults)
        {
            try
            {
                foreach (KeyValuePair<string, List<string>> kv in rollResults)
                {
                    int lF = 0;
                    int lIndex = 0;
                    while (lF < expr.Length && (lF = expr.IndexOf(kv.Key, lF)) != -1)
                    {
                        if (kv.Value.Count > lIndex)
                        {
                            lF += kv.Key.Length;
                            string t = $"[={kv.Value[lIndex++]}]";
                            expr = expr.Insert(lF, t);
                            lF += t.Length;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                // NOOP
            }
        }

        private static ChatBlockExpressionRollContents GetRollContents(List<(int, int)> data)
        {
            ChatBlockExpressionRollContents rollContents = ChatBlockExpressionRollContents.None;
            foreach ((int, int) roll in data)
            {
                if (roll.Item1 == 0)
                {
                    continue;
                }

                ChatBlockExpressionRollContents local = roll.Item2 switch
                {
                    4 => ChatBlockExpressionRollContents.SingleD4,
                    6 => ChatBlockExpressionRollContents.SingleD6,
                    8 => ChatBlockExpressionRollContents.SingleD8,
                    10 => ChatBlockExpressionRollContents.SingleD10,
                    12 => ChatBlockExpressionRollContents.SingleD12,
                    20 => ChatBlockExpressionRollContents.SingleD20,
                    100 => ChatBlockExpressionRollContents.SingleD100,
                    _ => ChatBlockExpressionRollContents.SingleDUnknown
                };

                if (roll.Item1 > 1 || (local <= ChatBlockExpressionRollContents.SingleDUnknown && rollContents.HasFlag(local)))
                {
                    local = (ChatBlockExpressionRollContents)((int)local << 8);
                }

                rollContents |= local;
            }

            return rollContents;
        }

        private static bool MoveNext(string s, ref int idx, out char c)
        {
            if (idx >= s.Length)
            {
                c = '\0';
                return false;
            }

            c = s[idx++];
            return true;
        }

        public static bool IsEscaped(string s, int idx) => idx != 0 && s[idx - 1] == '\\';

        public sealed class RollExpressionEvaluator
        {
            private readonly Expression _expression;

            public RollResultFlags ResultFlags { get; set; } = RollResultFlags.None;
            public Dictionary<string, List<string>> RollResults { get; } = new Dictionary<string, List<string>>();
            public List<(int, int)> RollsMade { get; } = new List<(int, int)>();
            public object EvaluationResult { get; set; } = null;

            public RollExpressionEvaluator(string expr)
            {
                this._expression = new Expression(expr);
                this._expression.EvaluateFunction += this.RollFunction;
            }

            public bool TryRun(out object result)
            {
                try
                {
                    this.EvaluationResult = result = this._expression.Evaluate();
                    return true;
                }
                catch (Exception e)
                {
                    this.EvaluationResult = result = e;
                    return false;
                }
            }

            private void RollFunction(string name, FunctionArgs args)
            {
                if (name.Equals("roll"))
                {
                    try
                    {
                        int num = Math.Min((int)args.Parameters[0].Evaluate(), 10000000);
                        int side = (int)args.Parameters[1].Evaluate();
                        string accumStr = string.Empty;
                        int accumInt = 0;
                        for (int i = 0; i < num; ++i)
                        {
                            int r = RandomNumberGenerator.GetInt32(side) + 1;
                            if (r == 1)
                            {
                                this.ResultFlags |= RollResultFlags.HadMinimumValue;
                            }

                            if (r == side)
                            {
                                this.ResultFlags |= RollResultFlags.HadMaximumValue;
                            }

                            accumStr += r.ToString() + (i != num - 1 ? ',' : "");
                            accumInt += r;
                        }

                        args.HasResult = true;
                        args.Result = accumInt;
                        this.AddRollResult($"{name}({num}, {side})", accumStr);
                        this.RollsMade.Add((num, side));
                    }
                    catch
                    {
                        args.HasResult = false;
                        args.Result = 0;
                    }
                }
            }

            private void AddRollResult(string expr, string val)
            {
                if (!this.RollResults.TryGetValue(expr, out List<string> results))
                {
                    this.RollResults[expr] = results = new List<string>();
                }

                results.Add(val);
            }
        }

        public enum BlockMode
        { 
            Text = -1,
            Error = 0,
            ColorSpecifier = 1,
            Skip = 2,
            TooltipSpecifier = 3,
            RenderModeSpecifier = 4,
            Expression = 5,
            Passthrough = 6,
            Recursive = 7,
            DestinationSpecifier = 8,
            SenderNameSpecifier = 9,
            SenderPortraitSpecifier = 10
        }

        [Flags]
        public enum RollResultFlags
        { 
            None = 0,
            HadMaximumValue = 0b01,
            HadMinimumValue = 0b10,
            HadMaximumAndMinimumValue = HadMaximumValue | HadMinimumValue
        }
    }
}
