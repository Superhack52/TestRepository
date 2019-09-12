namespace Union
{
    using System.Collections.Generic;
    using System.Linq.Expressions;

    public static class OperatorInfo
    {
        public static Dictionary<ExpressionType, string> OperatorMatches = MakeOperatorTable();

        private static Dictionary<ExpressionType, string> MakeOperatorTable()
        {
            var res = new Dictionary<ExpressionType, string>
            {
                [ExpressionType.Decrement] = "op_Decrement",
                [ExpressionType.Increment] = "op_Increment",
                [ExpressionType.Negate] = "op_UnaryNegation",
                [ExpressionType.UnaryPlus] = "op_UnaryPlus",
                [ExpressionType.Not] = "op_LogicalNot",
                [ExpressionType.IsTrue] = "op_True",
                [ExpressionType.IsFalse] = "op_False",
                [ExpressionType.OnesComplement] = "op_OnesComplement",
                [ExpressionType.Add] = "op_Addition",
                [ExpressionType.Subtract] = "op_Subtraction",
                [ExpressionType.Multiply] = "op_Multiply",
                [ExpressionType.Divide] = "op_Division",
                [ExpressionType.Modulo] = "op_Modulus",
                [ExpressionType.ExclusiveOr] = "op_ExclusiveOr",
                [ExpressionType.And] = "op_BitwiseAnd",
                [ExpressionType.Or] = "op_BitwiseOr",
                [ExpressionType.And] = "op_LogicalAnd",
                [ExpressionType.Or] = "op_LogicalOr",
                [ExpressionType.LeftShift] = "op_LeftShift",
                [ExpressionType.RightShift] = "op_RightShift",
                [ExpressionType.Equal] = "op_Equality",
                [ExpressionType.GreaterThan] = "op_GreaterThan",
                [ExpressionType.LessThan] = "op_LessThan",
                [ExpressionType.NotEqual] = "op_Inequality",
                [ExpressionType.GreaterThanOrEqual] = "op_GreaterThanOrEqual",
                [ExpressionType.LessThanOrEqual] = "op_LessThanOrEqual",
                [ExpressionType.MultiplyAssign] = "op_MultiplicationAssignment",
                [ExpressionType.SubtractAssign] = "op_SubtractionAssignment",
                [ExpressionType.ExclusiveOrAssign] = "op_ExclusiveOrAssignment",
                [ExpressionType.LeftShiftAssign] = "op_LeftShiftAssignment",
                [ExpressionType.RightShiftAssign] = "op_RightShiftAssignment",
                [ExpressionType.ModuloAssign] = "op_ModulusAssignment",
                [ExpressionType.AddAssign] = "op_AdditionAssignment",
                [ExpressionType.AndAssign] = "op_BitwiseAndAssignment",
                [ExpressionType.OrAssign] = "op_BitwiseOrAssignment",
                [ExpressionType.DivideAssign] = "op_DivisionAssignment"
            };

            return res;
        }
    }
}