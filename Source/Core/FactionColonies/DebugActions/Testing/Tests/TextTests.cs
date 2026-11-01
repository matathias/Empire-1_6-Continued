namespace FactionColonies
{
    public static class TextTests
    {
        // --- TextUtil.FloorStat ---

        [EmpireTest("Text")]
        public static void FloorStat_TruncatesToTwoDecimals()
        {
            TestAssert.AreEqual("1.23", TextUtil.FloorStat(1.239));
        }

        [EmpireTest("Text")]
        public static void FloorStat_WholeNumber()
        {
            TestAssert.AreEqual("5", TextUtil.FloorStat(5.0));
        }

        [EmpireTest("Text")]
        public static void FloorStat_FloorsNotRounds()
        {
            TestAssert.AreEqual("0.99", TextUtil.FloorStat(0.999));
        }

        // --- TextUtil.CleaveAtNewline ---

        [EmpireTest("Text")]
        public static void CleaveAtNewline_MiddleNewline_ReturnsFirstLine()
        {
            TestAssert.AreEqual("hello", TextUtil.CleaveAtNewline("hello\nworld"));
        }

        [EmpireTest("Text")]
        public static void CleaveAtNewline_NoNewline_ReturnsInput()
        {
            TestAssert.AreEqual("no newline", TextUtil.CleaveAtNewline("no newline"));
        }

        [EmpireTest("Text")]
        public static void CleaveAtNewline_LeadingNewline_SkipsToNextLine()
        {
            TestAssert.AreEqual("leading", TextUtil.CleaveAtNewline("\nleading"));
        }

        [EmpireTest("Text")]
        public static void CleaveAtNewline_MultipleLeadingNewlines_SkipsAll()
        {
            TestAssert.AreEqual("deep", TextUtil.CleaveAtNewline("\n\ndeep"));
        }

        [EmpireTest("Text")]
        public static void CleaveAtNewline_OnlyNewline_ReturnsEmpty()
        {
            TestAssert.AreEqual("", TextUtil.CleaveAtNewline("\n"));
        }

        [EmpireTest("Text")]
        public static void CleaveAtNewline_EmptyString_ReturnsEmpty()
        {
            TestAssert.AreEqual("", TextUtil.CleaveAtNewline(""));
        }

        // --- TextGen.ToShortName ---

        [EmpireTest("Text")]
        public static void ToShortName_TwoWords_FirstPlusInitial()
        {
            TestAssert.AreEqual("Imperial G", TextUtil.ToShortName("Imperial Guard"));
        }

        [EmpireTest("Text")]
        public static void ToShortName_SingleWord_ReturnsWordWithSpace()
        {
            // Single capitalized word: Aggregate processes it against itself, appending a space
            TestAssert.AreEqual("North ", TextUtil.ToShortName("North"));
        }

        [EmpireTest("Text")]
        public static void ToShortName_ThreeWords_FirstPlusInitials()
        {
            TestAssert.AreEqual("The CF", TextUtil.ToShortName("The Crimson Fleet"));
        }

        [EmpireTest("Text")]
        public static void ToShortName_Null_ReturnsNullWithoutThrowing()
        {
            TestAssert.AreEqual(null, TextUtil.ToShortName(null));
        }

        [EmpireTest("Text")]
        public static void ToShortName_Empty_ReturnsEmptyWithoutThrowing()
        {
            TestAssert.AreEqual("", TextUtil.ToShortName(""));
        }

        // --- TextUtil.ColorizeAdditiveBonus ---

        [EmpireTest("Text")]
        public static void ColorizeAdditive_PositiveBonus_ContainsPlusSign()
        {
            string result = TextUtil.ColorizeAdditiveBonus(5.0);
            TestAssert.IsTrue(result.Contains("+"), $"Expected '+' in '{result}'");
        }

        [EmpireTest("Text")]
        public static void ColorizeAdditive_NegativeBonus_NoPlusSign()
        {
            string result = TextUtil.ColorizeAdditiveBonus(-3.0);
            TestAssert.IsFalse(result.Contains("+"), $"Expected no '+' in '{result}'");
        }

        [EmpireTest("Text")]
        public static void ColorizeAdditive_HardInvert_FlipsSign()
        {
            // 5.0 with hardinvert becomes -5, so no plus sign
            string result = TextUtil.ColorizeAdditiveBonus(5.0, hardinvert: true);
            TestAssert.IsTrue(result.Contains("-"), $"Expected '-' in '{result}'");
        }

        // --- TextUtil.ColorizeMultiplierBonus ---

        [EmpireTest("Text")]
        public static void ColorizeMultiplier_WithXSign_ContainsX()
        {
            string result = TextUtil.ColorizeMultiplierBonus(1.5);
            TestAssert.IsTrue(result.Contains("x"), $"Expected 'x' in '{result}'");
        }

        [EmpireTest("Text")]
        public static void ColorizeMultiplier_WithoutXSign_NoX()
        {
            string result = TextUtil.ColorizeMultiplierBonus(1.5, addXsign: false);
            TestAssert.IsFalse(result.Contains("x"), $"Expected no 'x' in '{result}'");
        }
    }
}
