// MIT License
// 
// Copyright (c) 2024 Adam Foflonker
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// THIS FILE IS A VERSION OF ImGuiDatePicker (https://github.com/DnA-IntRicate/ImGuiDatePicker)
// SLIGHTLY MODIFIED AND PORTED TO C#! THIS IS NOT THE ORIGINAL VERSION!
// ORIGINAL CODE CAN BE FOUND HERE https://github.com/DnA-IntRicate/ImGuiDatePicker/blob/master/ImGuiDatePicker.cpp
namespace VTT.Render
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using VTT.Util;

    public static class ImGuiDatePicker
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string TimePointToLongString(SimpleLanguage lang, DateTime dt) => $"{dt.Day} {lang.Translate($"generic.month.{dt.Month}")} {dt.Year}";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMinDate(DateTime dt) => dt.Month == 1 && dt.Year == DateTime.UnixEpoch.Year;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMaxDate(DateTime dt) => dt.Month == 12 && dt.Year == DateTime.MaxValue.Year;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DayOfWeek(int dayOfMonth, int month, int year)
        {
            if (month is 1 or 2)
            {
                month += 12;
                year -= 1;
            }

            int h = (dayOfMonth
                + (int)(Math.Floor(13 * (month + 1) / 5.0))
                + year
                + (int)(Math.Floor(year / 4.0))
                - (int)(Math.Floor(year / 100.0))
                + (int)(Math.Floor(year / 400.0))) % 7;

            return ((h + 5) % 7) + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NumDaysInMonth(int month, int year) => DateTime.DaysInMonth(year, month);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NumWeeksInMonth(int month, int year)
        {
            int days = NumDaysInMonth(month, year);
            int firstDay = DayOfWeek(1, month, year);

            return (int)Math.Ceiling((days + firstDay - 1) / 7.0);
        }

        private static Span<int> CalendarWeek(int week, int startDay, int daysInMonth)
        {
            Span<int> res = new int[7];
            int startOfWeek = (7 * (week - 1)) + 2 - startDay;

            if (startOfWeek >= 1)
            {
                res[0] = startOfWeek;
            }

            for (int i = 1; i < 7; ++i)
            {
                int day = startOfWeek + i;
                if ((day >= 1) && (day <= daysInMonth))
                    res[i] = day;
            }

            return res;
        }

        public static unsafe bool DatePicker(string label, ref DateTime v, SimpleLanguage lang, bool clampToBorder, float itemSpacing)
        {
            bool res = false;

            bool hiddenLabel = label[..2] == "##";
            string myLabel = hiddenLabel ? label[2..] : label;
            string displayLabel = hiddenLabel ? label[2..] : label;
            if (label.Contains("###", StringComparison.InvariantCultureIgnoreCase))
            {
                myLabel = label[(label.IndexOf("###") + 3)..];
                displayLabel = label[..label.IndexOf("###")];
            }

            if (!hiddenLabel)
            {
                ImGui.TextUnformatted(displayLabel);
                ImGui.SameLine((itemSpacing == 0.0f) ? 0.0f : ImGui.GetCursorPos().X + itemSpacing);
            }

            if (clampToBorder)
            {
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            }

            Vector2 windowSize = new(274.5f, 301.5f);
            ImGui.SetNextWindowSize(windowSize);

            if (ImGui.BeginCombo("##" + myLabel, TimePointToLongString(lang, v)))
            {
                int monthIdx = v.Month - 1;
                int year = v.Year;

                ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X * 0.5f));

                string[] months = new string[] { 
                    lang.Translate("generic.month.1"),
                    lang.Translate("generic.month.2"),
                    lang.Translate("generic.month.3"),
                    lang.Translate("generic.month.4"),
                    lang.Translate("generic.month.5"),
                    lang.Translate("generic.month.6"),
                    lang.Translate("generic.month.7"),
                    lang.Translate("generic.month.8"),
                    lang.Translate("generic.month.9"),
                    lang.Translate("generic.month.10"),
                    lang.Translate("generic.month.11"),
                    lang.Translate("generic.month.12")
                };

                if (ImGui.Combo("##CmbMonth_" + myLabel, ref monthIdx, months, 12))
                {
                    v = v.AddMonths(monthIdx - v.Month);
                    res = true;
                }

                ImGui.PopItemWidth();
                ImGui.SameLine();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);

                if (ImGui.InputInt("##IntYear_" + myLabel, ref year))
                {
                    v = v.AddYears(year - v.Year);
                    res = true;
                }

                ImGui.PopItemWidth();

                float contentWidth = ImGui.GetContentRegionAvail().X;
                float arrowSize = ImGui.GetFrameHeight();
                float arrowButtonWidth = (arrowSize * 2.0f) + ImGui.GetStyle().ItemSpacing.X;
                float bulletSize = arrowSize - 5.0f;
                float bulletButtonWidth = bulletSize + ImGui.GetStyle().ItemSpacing.X;
                float combinedWidth = arrowButtonWidth + bulletButtonWidth;
                float offset = (contentWidth - combinedWidth) * 0.5f;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 20.0f);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                ImGui.BeginDisabled(IsMinDate(v));

                if (ImGui.Button("◂###ArrowLeft_" + myLabel, new Vector2(arrowSize, arrowSize)))
                {
                    v = v.AddMonths(-1);
                    res = true;
                }

                ImGui.EndDisabled();
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, *ImGui.GetStyleColorVec4(ImGuiCol.Text));
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2.0f);

                if (ImGui.Button("##ArrowMid_" + myLabel, new Vector2(bulletSize, bulletSize)))
                {
                    v = DateTime.Now;
                    res = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                ImGui.BeginDisabled(IsMaxDate(v));

                if (ImGui.Button("▸###ArrowRight_" + myLabel, new Vector2(arrowSize, arrowSize)))
                {
                    v = v.AddMonths(1);
                    res = true;
                }

                ImGui.EndDisabled();
                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar();

                ImGuiTableFlags TABLE_FLAGS = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoHostExtendY;
                string[] days = new string[] { 
                    lang.Translate("generic.day.1"),
                    lang.Translate("generic.day.2"),
                    lang.Translate("generic.day.3"),
                    lang.Translate("generic.day.4"),
                    lang.Translate("generic.day.5"),
                    lang.Translate("generic.day.6"),
                    lang.Translate("generic.day.7"),
                };

                if (ImGui.BeginTable("##Table_" + myLabel, 7, TABLE_FLAGS, ImGui.GetContentRegionAvail()))
                {
                    foreach (string day in days)
                    {
                        ImGui.TableSetupColumn(day, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHeaderWidth, 30.0f);
                    }

                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, *ImGui.GetStyleColorVec4(ImGuiCol.TableHeaderBg));
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive,  *ImGui.GetStyleColorVec4(ImGuiCol.TableHeaderBg));
                    ImGui.TableHeadersRow();
                    ImGui.PopStyleColor(2);

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    int month = monthIdx + 1;
                    int firstDayOfMonth = DayOfWeek(1, month, year);
                    int numDaysInMonth = NumDaysInMonth(month, year);
                    int numWeeksInMonth = NumWeeksInMonth(month, year);

                    for (int i = 1; i <= numWeeksInMonth; ++i)
                    {
                        foreach (int day in CalendarWeek(i, firstDayOfMonth, numDaysInMonth))
                        {
                            if (day != 0)
                            {
                                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 20.0f);

                                bool selected = day == v.Day;
                                if (!selected)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                                    ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                                }

                                if (ImGui.Button(day.ToString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeightWithSpacing() + 5.0f)))
                                {
                                    v = new DateTime(year, month, day, v.Hour, v.Minute, v.Second, v.Millisecond, v.Kind);
                                    res = true;
                                    ImGui.CloseCurrentPopup();
                                }

                                if (!selected)
                                {
                                    ImGui.PopStyleColor(2);
                                }

                                ImGui.PopStyleVar();
                            }

                            if (day != numDaysInMonth)
                            {
                                ImGui.TableNextColumn();
                            }
                        }
                    }

                    ImGui.EndTable();
                }

                ImGui.EndCombo();
            }

            return res;
        }
    }
}
