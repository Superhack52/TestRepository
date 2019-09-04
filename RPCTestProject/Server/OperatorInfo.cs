using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;
namespace ServerRPC
{
    public static class OperatorInfo
    {

    


         public static Dictionary<ExpressionType, string> OperatorMatches = MakeOperatorTable();

        private static Dictionary<ExpressionType, string> MakeOperatorTable()
        {
            var res = new Dictionary<ExpressionType, string>();



            // unary ExpressionType as defined in Partition I Architecture 9.3.1:

            res[ExpressionType.Decrement] = "op_Decrement";      // --

            res[ExpressionType.Increment] = "op_Increment";      // ++

            res[ExpressionType.Negate] = "op_UnaryNegation";         // - (unary)

            res[ExpressionType.UnaryPlus] = "op_UnaryPlus";           // + (unary)

            res[ExpressionType.Not] = "op_LogicalNot";             // !

            res[ExpressionType.IsTrue] = "op_True";             // not defined

            res[ExpressionType.IsFalse] = "op_False";             // not defined

            //res.Add(AddressOf,           "op_AddressOf",                 null);             // & (unary)

            res[ExpressionType.OnesComplement] = "op_OnesComplement"; // ~

            //res.Add(PointerDereference,  "op_PointerDereference",        null);             // * (unary)



            // binary ExpressionType as defined in Partition I Architecture 9.3.2:

            res[ExpressionType.Add] = "op_Addition";           // +

            res[ExpressionType.Subtract] = "op_Subtraction";       // -

            res[ExpressionType.Multiply] = "op_Multiply";       // *

            res[ExpressionType.Divide] = "op_Division";         // /

            res[ExpressionType.Modulo] = "op_Modulus";            // %

            res[ExpressionType.ExclusiveOr] = "op_ExclusiveOr";    // ^

            res[ExpressionType.And] = "op_BitwiseAnd";     // &

            res[ExpressionType.Or] = "op_BitwiseOr";      // |

            res[ExpressionType.And] = "op_LogicalAnd";            // &&

            res[ExpressionType.Or] = "op_LogicalOr";             // ||

            res[ExpressionType.LeftShift] = "op_LeftShift";      // <<

            res[ExpressionType.RightShift] = "op_RightShift";     // >>

            res[ExpressionType.Equal] = "op_Equality";         // ==   

            res[ExpressionType.GreaterThan] = "op_GreaterThan";    // >

            res[ExpressionType.LessThan] = "op_LessThan";       // <

            res[ExpressionType.NotEqual] = "op_Inequality";      // != 

            res[ExpressionType.GreaterThanOrEqual] = "op_GreaterThanOrEqual";        // >=

            res[ExpressionType.LessThanOrEqual] = "op_LessThanOrEqual";        // <=

            res[ExpressionType.MultiplyAssign] =  "op_MultiplicationAssignment";       // *=

            res[ExpressionType.SubtractAssign] =  "op_SubtractionAssignment";       // -=

            res[ExpressionType.ExclusiveOrAssign] = "op_ExclusiveOrAssignment";            // ^=

            res[ExpressionType.LeftShiftAssign] = "op_LeftShiftAssignment";      // <<=

            res[ExpressionType.RightShiftAssign] = "op_RightShiftAssignment";     // >>=

            res[ExpressionType.ModuloAssign] = "op_ModulusAssignment";            // %=

            res[ExpressionType.AddAssign] = "op_AdditionAssignment";            // += 

            res[ExpressionType.AndAssign] = "op_BitwiseAndAssignment";     // &=

            res[ExpressionType.OrAssign] =   "op_BitwiseOrAssignment";      // |=

            res[ExpressionType.DivideAssign] = "op_DivisionAssignment";         // /=



            return res;

        }

    }
}
