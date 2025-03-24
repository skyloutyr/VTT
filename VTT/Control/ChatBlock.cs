namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using VTT.Util;

    public class ChatBlock
    {
        private Color color;

        public string Text { get; set; }
        public string Tooltip { get; set; }
        public Color Color
        {
            get => this.color.Rgba() == 0 ? Extensions.FromAbgr(ImGuiNET.ImGui.GetColorU32(ImGuiNET.ImGuiCol.Text)) : this.color;
            set => this.color = value;
        }

        public ChatBlockType Type { get; set; }
        public ChatBlockExpressionRollContents RollContents { get; set; }
        public bool DoNotPersist { get; set; }

        public void WriteV1(BinaryWriter bw)
        {
            bw.Write(this.Text);
            bw.Write(this.Tooltip);
            bw.Write(this.color.Argb());
            bw.Write((byte)this.Type);
        }

        public void WriteV2(BinaryWriter bw)
        {
            this.WriteV1(bw);
            bw.Write((ushort)this.RollContents);
        }

        public void ReadV1(BinaryReader br)
        {
            this.Text = br.ReadString();
            this.Tooltip = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.Type = (ChatBlockType)br.ReadByte();
        }

        public void ReadV2(BinaryReader br)
        {
            this.ReadV1(br);
            this.RollContents = (ChatBlockExpressionRollContents)br.ReadUInt16();
        }

        private static readonly Regex rollSearchExpr = new Regex(@"roll\(([0-9]+), *([0-9]+)\)", RegexOptions.Compiled);
        public void TryGuessExpressionRollContents()
        {
            if (this.Type.HasFlag(ChatBlockType.Expression) && !string.IsNullOrEmpty(this.Tooltip))
            {
                MatchCollection mc = rollSearchExpr.Matches(this.Tooltip);
                if (mc.Count > 0)
                {
                    ChatBlockExpressionRollContents contents = ChatBlockExpressionRollContents.None;
                    for (int i = 0; i < mc.Count; ++i)
                    {
                        Match m = mc[i];
                        // For whatever reason c# creates 3 groups per match, group 0 is the entire match, groups 1+ are individual matches
                        if (!m.Success || m.Groups.Count != 3)
                        {
                            continue;
                        }

                        if (int.TryParse(m.Groups[1].Value, out int nRollsV) && int.TryParse(m.Groups[2].Value, out int rKindV) && nRollsV > 0)
                        {
                            switch (rKindV)
                            {
                                case 2:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD2, nRollsV);
                                    break;
                                }

                                case 4:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD4, nRollsV);
                                    break;
                                }

                                case 6:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD6, nRollsV);
                                    break;
                                }

                                case 8:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD8, nRollsV);
                                    break;
                                }

                                case 10:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD10, nRollsV);
                                    break;
                                }

                                case 12:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD12, nRollsV);
                                    break;
                                }

                                case 20:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD20, nRollsV);
                                    break;
                                }

                                case 100:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD100, nRollsV);
                                    break;
                                }

                                default:
                                {
                                    AddRollKind(ref contents, ChatBlockExpressionRollContents.SingleD20, nRollsV);
                                    break;
                                }
                            }
                        }
                    }

                    this.RollContents = contents;
                }
            }
        }

        private static void AddRollKind(ref ChatBlockExpressionRollContents contents, ChatBlockExpressionRollContents flagToAdd, int amt)
        {
            if (flagToAdd <= ChatBlockExpressionRollContents.SingleD2 && amt > 1)
            {
                flagToAdd = (ChatBlockExpressionRollContents)((int)flagToAdd << 8);
            }

            if (flagToAdd <= ChatBlockExpressionRollContents.SingleD2)
            {
                ChatBlockExpressionRollContents multiples = (ChatBlockExpressionRollContents)((int)flagToAdd << 8);
                if (!contents.HasFlag(multiples))
                {
                    if (contents.HasFlag(flagToAdd))
                    {
                        contents &= ~flagToAdd;
                        contents |= multiples;
                    }
                    else
                    {
                        contents |= flagToAdd;
                    }
                }
            }
            else
            {
                contents |= flagToAdd;
            }
        }
    }

    [Flags]
    public enum ChatBlockType
    {
        Text = 1,
        Expression = 2,
        Error = 4,
        Compound = 8,
        Image = 16,

        ExpressionError = Expression | Error,
        TextError = Text | Error,
    }

    [Flags]
    public enum ChatBlockExpressionRollContents
    {
        None = 0,
        SingleD4 = 1,
        SingleD6 = 2,
        SingleD8 = 4,
        SingleD10 = 8,
        SingleD12 = 16,
        SingleD20 = 32,
        SingleD100 = 64,
        SingleD2 = 128,
        MultipleD4 = 256,
        MultipleD6 = 512,
        MultipleD8 = 1024,
        MultipleD10 = 2048,
        MultipleD12 = 4096,
        MultipleD20 = 8192,
        MultipleD100 = 16384,
        MultipleD2 = 32768,
        AnyD4 = SingleD4 | MultipleD4,
        AnyD6 = SingleD6 | MultipleD6,
        AnyD8 = SingleD8 | MultipleD8,
        AnyD10 = SingleD10 | MultipleD10,
        AnyD12 = SingleD12 | MultipleD12,
        AnyD20 = SingleD20 | MultipleD20,
        AnyD100 = SingleD100 | MultipleD100,
        AnyD2 = SingleD2 | MultipleD2
    }
}