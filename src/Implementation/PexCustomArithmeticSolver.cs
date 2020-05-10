using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Reasoning.ArithmeticSolving;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Monitoring;
using System.IO;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Coverage;

namespace PexCustomArithmeticSolvers.Implementation
{
    [__DoNotInstrument]
    public abstract class PexCustomArithmeticSolver
        : IArithmeticSolver,
        IDisposable
    {
        private delegate Term TermOp(Term left, Term right);

        private readonly string name;
        private readonly string explorationName;

        private readonly int K = 1; //failure constant, e.g. for != comparisons, and added as delta for >, < comparison

        protected IArithmeticSolvingContext context;
        protected IArithmeticModelBuilder modelBuilder;
        protected TermManager termManager;
        protected Random random;

        protected LogManager logManager;

        private bool initialized;

        private readonly Method method;

        private readonly bool isLoggingEnabled;
        public bool IsLoggingEnabled
        {
            get { return this.isLoggingEnabled; }
        }

        private readonly bool isLoggingVerboseEnabled;
        public bool IsLoggingVerboseEnabled
        {
            get { return this.isLoggingVerboseEnabled; }
        }

        protected readonly int fitnessBudget;
        protected int fitnessEvaluations;

        public string ExplorationName
        {
            get { return this.explorationName; }
        }

        private DateTime startTime;

        public abstract TryGetModelResult TryGetArithmeticModel(IArithmeticSolvingContext context, out IArithmeticModel model);

        public PexCustomArithmeticSolver(
            string explorationName,
            bool isLoggingEnabled,
            bool isLoggingVerboseEnabled,
            int fitnessBudget,
            string name,
            Method m)
        {
            this.explorationName = explorationName;
            this.isLoggingEnabled = isLoggingEnabled;
            this.isLoggingVerboseEnabled = isLoggingVerboseEnabled;
            this.fitnessBudget = fitnessBudget;
            this.fitnessEvaluations = 0;
            this.name = name;
            this.initialized = false;
            this.method = m;
        }

        public void InitializeCustomSolver(IArithmeticSolvingContext context)
        {
            this.context = context;
            this.modelBuilder = context.CreateArithmeticModelBuilder(context.InitialModel);
            this.startTime = DateTime.Now;
            this.fitnessEvaluations = 0;
            this.termManager = context.Host.ExplorationServices.TermManager;
            this.random = context.Random;

            if (!this.initialized && this.isLoggingEnabled)
            {
                this.logManager = new LogManager(this.name + "_" + this.explorationName, context);
                this.initialized = true;
            }
        }

        #region Model helper functions

        protected double EvaluateArithmeticModel(ref IArithmeticModel model)
        {
            double distance = 0.0;
            double fitnessValue = 0.0;

            Term innerTerm;
            SafeSet<Term> visited = new SafeSet<Term>();

            this.fitnessEvaluations++;

            foreach (var variable in this.context.Variables)
            {
                foreach (var constraint in variable.Constraints)
                {
                    if (!visited.Add(constraint))
                        continue;

                    if (this.termManager.TryGetInnerLogicallyNegatedValue(constraint, out innerTerm))
                    {
                        distance = EvaluateConstraint(ref model, innerTerm, true);
                    }
                    else
                    {
                        distance = EvaluateConstraint(ref model, constraint, false);
                    }

                    if (distance == Double.MaxValue)
                    {
                        fitnessValue = distance;
                        break;
                    }
                    else
                        fitnessValue += distance;
                }
            }

            visited.ClearAndTrim();
            visited = null;

            return fitnessValue;
        }

        protected void UpdateModelBuilder(ref IArithmeticModel model)
        {
            this.modelBuilder = this.context.CreateArithmeticModelBuilder(model);
        }

        #endregion

        #region Randomizing Variables

        protected void RandomizingInputVariablesWithGaussian()
        {
            Term randomValueTerm = null;
            foreach (var variable in this.context.Variables)
            {
                var layout = this.termManager.GetLayout(variable.Term);
                var layoutKind = layout.Kind;
                double rand = Gaussian();

                switch (layoutKind)
                {
                    case LayoutKind.I1:
                        randomValueTerm = this.termManager.I1(Convert.ToByte(rand));
                        break;
                    case LayoutKind.I2:
                        randomValueTerm = this.termManager.I2(Convert.ToInt16(rand));
                        break;
                    case LayoutKind.I4:
                        randomValueTerm = this.termManager.I4(Convert.ToInt32(rand));
                        break;
                    case LayoutKind.I8:
                        randomValueTerm = this.termManager.I8(Convert.ToInt64(rand));
                        break;
                    case LayoutKind.R4:
                        randomValueTerm = this.termManager.R4(Convert.ToSingle(rand));
                        break;
                    case LayoutKind.R8:
                        randomValueTerm = this.termManager.R8(Convert.ToDouble(rand));
                        break;
                    case LayoutKind.Struct:
                        if (layout.StructType.IsDecimalType)
                        {
                            randomValueTerm = this.termManager.Decimal(Convert.ToDecimal(rand));
                        }
                        else
                        {
                            randomValueTerm = null;
                        }
                        break;
                    default: randomValueTerm = null; break;
                }

                if (randomValueTerm != null)
                    this.modelBuilder.TryAssign(variable, randomValueTerm);
            }
        }

        protected void RandomizeInputVariablesWithBounds(int min, int max)
        {
            Term randomValueTerm = null;
            foreach (var variable in this.context.Variables)
            {
                var layout = this.termManager.GetLayout(variable.Term);
                var layoutKind = layout.Kind;
                int rand = this.random.Next(min, max);

                switch (layoutKind)
                {
                    case LayoutKind.I1:
                        randomValueTerm = this.termManager.I1(Convert.ToByte(rand));
                        break;
                    case LayoutKind.I2:
                        randomValueTerm = this.termManager.I2(Convert.ToInt16(rand));
                        break;
                    case LayoutKind.I4:
                        randomValueTerm = this.termManager.I4(Convert.ToInt32(rand));
                        break;
                    case LayoutKind.I8:
                        randomValueTerm = this.termManager.I8(Convert.ToInt64(rand));
                        break;
                    case LayoutKind.R4:
                        randomValueTerm = this.termManager.R4(Convert.ToSingle(rand));
                        break;
                    case LayoutKind.R8:
                        randomValueTerm = this.termManager.R8(Convert.ToDouble(rand));
                        break;
                    case LayoutKind.Struct:
                        if (layout.StructType.IsDecimalType)
                        {
                            randomValueTerm = this.termManager.Decimal(Convert.ToDecimal(rand));
                        }
                        else
                        {
                            randomValueTerm = null;
                        }
                        break;
                    default: randomValueTerm = null; break;
                }

                if (randomValueTerm != null)
                    this.modelBuilder.TryAssign(variable, randomValueTerm);
            }
        }

        protected void RandomizeInputVariablesBitConverter()
        {
            byte[] eightBytes = new byte[8];
            Term randomValueTerm = null;

            foreach (var variable in this.context.Variables)
            {
                var layout = this.termManager.GetLayout(variable.Term);
                var layoutKind = layout.Kind;
                this.random.NextBytes(eightBytes);

                switch (layoutKind)
                {
                    case LayoutKind.I1:
                        randomValueTerm = this.termManager.I1(eightBytes[0]);
                        break;
                    case LayoutKind.I2:
                        randomValueTerm = this.termManager.I2(
                                    BitConverter.ToInt16(eightBytes, 0));
                        break;
                    case LayoutKind.I4:
                        randomValueTerm = this.termManager.I4(
                                    BitConverter.ToInt32(eightBytes, 0));
                        break;
                    case LayoutKind.I8:
                        randomValueTerm = this.termManager.I8(
                                    BitConverter.ToInt64(eightBytes, 0));
                        break;
                    case LayoutKind.R4:
                        randomValueTerm = this.termManager.R4(
                                    BitConverter.ToSingle(eightBytes, 0));
                        break;
                    case LayoutKind.R8:
                        randomValueTerm = this.termManager.R8(
                                    BitConverter.ToDouble(eightBytes, 0));
                        break;
                    case LayoutKind.Struct:
                        if (layout.StructType.IsDecimalType)
                        {
                            using (_ProtectingThreadContext.Acquire())
                            {
                                randomValueTerm = this.termManager.Decimal(new decimal(
                                        BitConverter.ToInt32(eightBytes, 0),
                                        BitConverter.ToInt32(eightBytes, 0),
                                        BitConverter.ToInt32(eightBytes, 0),
                                        this.random.Next(2) != 0,
                                        (byte)this.random.Next(29)));
                            }
                        }
                        else
                        {
                            randomValueTerm = null;
                        }
                        break;
                    default: randomValueTerm = null; break;
                }

                if (randomValueTerm != null)
                    this.modelBuilder.TryAssign(variable, randomValueTerm);
            }
        }

        protected void RandomizeInputVariableBitConverter(ref IArithmeticVariable variable)
        {
            byte[] eightBytes = new byte[8];
            Term randomValueTerm = null;
            var layout = this.termManager.GetLayout(variable.Term);
            var layoutKind = layout.Kind;
            this.random.NextBytes(eightBytes);

            switch (layoutKind)
            {
                case LayoutKind.I1:
                    randomValueTerm = this.termManager.I1(eightBytes[0]);
                    break;
                case LayoutKind.I2:
                    randomValueTerm = this.termManager.I2(
                                BitConverter.ToInt16(eightBytes, 0));
                    break;
                case LayoutKind.I4:
                    randomValueTerm = this.termManager.I4(
                                BitConverter.ToInt32(eightBytes, 0));
                    break;
                case LayoutKind.I8:
                    randomValueTerm = this.termManager.I8(
                                BitConverter.ToInt64(eightBytes, 0));
                    break;
                case LayoutKind.R4:
                    randomValueTerm = this.termManager.R4(
                                BitConverter.ToSingle(eightBytes, 0));
                    break;
                case LayoutKind.R8:
                    randomValueTerm = this.termManager.R8(
                                BitConverter.ToDouble(eightBytes, 0));
                    break;
                case LayoutKind.Struct:
                    if (layout.StructType.IsDecimalType)
                    {
                        using (_ProtectingThreadContext.Acquire())
                        {
                            randomValueTerm = this.termManager.Decimal(new decimal(
                                    BitConverter.ToInt32(eightBytes, 0),
                                    BitConverter.ToInt32(eightBytes, 0),
                                    BitConverter.ToInt32(eightBytes, 0),
                                    this.random.Next(2) != 0,
                                    (byte)this.random.Next(29)));
                        }
                    }
                    else
                    {
                        randomValueTerm = null;
                    }
                    break;
                case LayoutKind.Uniform:

                    break;
                default: randomValueTerm = null; break;
            }

            if (randomValueTerm != null)
                this.modelBuilder.TryAssign(variable, randomValueTerm);
        }

        #endregion

        #region Math helper functions
        // random number with standard Gaussian distribution
        public double Gaussian()
        {
            double U = this.random.NextDouble();
            double V = this.random.NextDouble();

            return Math.Sin(2 * Math.PI * V) * Math.Sqrt((-2 * Math.Log(1 - U)));
        }

        // random number with Gaussian distribution of mean mu and stddev sigma
        public double Gaussian(double mu, double sigma)
        {
            return mu + sigma * Gaussian();
        }
        #endregion

        #region Term manager helpers
        protected bool TryAddDoubleToTerm(Term original, double delta, out Term sum)
        {
            Layout layout = this.termManager.GetLayout(original);
            Term tmp;
            if (layout == Layout.I1)
            {
                tmp = this.termManager.Widen(original, StackWidening.FromI1);
                layout = Layout.I4;
            }
            else if (layout == Layout.I2)
            {
                tmp = this.termManager.Widen(original, StackWidening.FromI2);
                layout = Layout.I4;
            }
            else if (layout == Layout.R4)
            {
                tmp = this.termManager.Widen(original, StackWidening.FromR4);
                layout = Layout.R8;
            }
            else if (layout == Layout.I4 || layout == Layout.I8 || layout == Layout.R8)
            {
                tmp = original;
            }
            else
            {
                if (IsDecimalTerm(original))
                {
                    decimal dOld;
                    if (this.termManager.TryGetDecimalConstant(original, out dOld))
                    {
                        try
                        {
                            dOld += Convert.ToDecimal(delta);
                        }
                        catch (OverflowException)
                        {
                            if (delta < 0)
                                dOld = Decimal.MinValue;
                            else
                                dOld = Decimal.MaxValue;
                        }
                        finally
                        {
                            sum = this.termManager.Decimal(dOld);
                        }
                        return true;
                    }
                }
                sum = null; return false;
            }

            if (layout == Layout.I4)
            {
                sum = this.termManager.Add(tmp, this.termManager.I4((int)delta));
                return true;
            }
            else if (layout == Layout.R8)
            {
                sum = this.termManager.Add(original, this.termManager.R8(delta));
                return true;
            }
            else
            {
                sum = null; return false;
            }
        }

        protected bool IsDecimalTerm(Term term)
        {
            var layoutKind = this.termManager.GetLayoutKind(term);
            if (layoutKind == LayoutKind.Struct
                && this.termManager.GetLayout(term).StructType.IsDecimalType)
                return true;
            else
                return false;
        }
        #endregion

        #region Constraint evaluation functions
        protected double EvaluateConstraint(ref IArithmeticModel model, Term constraint, bool isNegated)
        {
            Term left, right;
            BinaryOperator @operator;
            if (this.termManager.TryGetBinary(constraint, out @operator, out left, out right))
            {

                TryDecomposeDecimalComparer(ref model, right, ref left, ref right);

                if (@operator == BinaryOperator.Ceq)
                {
                    return EqualsDistance(left, right, isNegated, ref model);
                }
                else
                {
                    return LessThanDistance(left, right, isNegated, ref model);
                }
            }
            return this.K;
        }

        private bool TryDecomposeDecimalComparer(ref IArithmeticModel model, Term value, ref Term leftValue, ref Term rightValue)
        {
            IFunction f;
            Term t;
            Term[] args;
            TypeEx fdt;
            if (this.termManager.TryGetFunctionApplication(value, out f, out t, out args) &&
                f.Method != null &&
                f.Method.TryGetDeclaringType(out fdt)
                && fdt == SystemTypes.Decimal
                && args.Length == 2)
            {
                leftValue = args[0];
                rightValue = args[1];
                return true;

            }
            return false;
        }

        #endregion

        #region Local branch distance functions
        private bool TryConvertTermToDouble(Term diff, out double result)
        {
            Layout layout = this.termManager.GetLayout(diff);

            if (layout == Layout.I1)
            {
                byte oByte;
                if (this.termManager.TryGetI1Constant(diff, out oByte))
                {
                    result = (double)oByte;
                    return true;
                }
            }
            else if (layout == Layout.I2)
            {
                short oShort;
                if (this.termManager.TryGetI2Constant(diff, out oShort))
                {
                    result = (double)oShort;
                    return true;
                }
            }
            else if (layout == Layout.I4)
            {
                int oInt;
                if (this.termManager.TryGetI4Constant(diff, out oInt))
                {
                    result = (double)oInt;
                    return true;
                }
            }
            else if (layout == Layout.I8)
            {
                long oLong;
                if (this.termManager.TryGetI8Constant(diff, out oLong))
                {
                    result = (double)oLong;
                    return true;
                }
            }
            else if (layout == Layout.R4)
            {
                float oFloat;
                if (this.termManager.TryGetR4Constant(diff, out oFloat))
                {
                    result = (double)oFloat;
                    return true;
                }
            }
            else if (layout == Layout.R8)
            {
                double oDouble;
                if (this.termManager.TryGetR8Constant(diff, out oDouble))
                {
                    result = oDouble;
                    return true;
                }
            }
            else
            {
                if (IsDecimalTerm(diff))
                {
                    decimal oDecimal;
                    if (this.termManager.TryGetDecimalConstant(diff, out oDecimal))
                    {
                        result = Convert.ToDouble(oDecimal);
                        return true;
                    }
                }
            }
            result = Double.MaxValue;
            return false;
        }

        private bool TrySubtractTwoTerms(Term left, Term right, out Term result)
        {
            Layout layout = this.termManager.GetLayout(left);
            Term tLeft, tRight;
            if (layout == Layout.I1)
            {
                byte bLeft, bRight;
                if (this.termManager.TryGetI1Constant(left, out bLeft)
                    && this.termManager.TryGetI1Constant(right, out bRight))
                {
                    result = this.termManager.I1((byte)(bLeft - bRight));
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
            else if (layout == Layout.I2)
            {
                short sLeft, sRight;
                if (this.termManager.TryGetI2Constant(left, out sLeft)
                    && this.termManager.TryGetI2Constant(right, out sRight))
                {
                    result = this.termManager.I2((short)(sLeft - sRight));
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
            else if (layout == Layout.R4)
            {
                float fLeft, fRight;
                if (this.termManager.TryGetR4Constant(left, out fLeft)
                    && this.termManager.TryGetR4Constant(right, out fRight))
                {
                    result = this.termManager.R4((float)(fLeft - fRight));
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
            else if (layout == Layout.I4 || layout == Layout.I8 || layout == Layout.R8)
            {
                tLeft = left;
                tRight = right;
            }
            else
            {
                if (IsDecimalTerm(left))
                {
                    decimal decLeft, decRight;
                    if (this.termManager.TryGetDecimalConstant(left, out decLeft)
                        && this.termManager.TryGetDecimalConstant(right, out decRight))
                    {
                        result = this.termManager.Decimal(decLeft - decRight);
                        return true;
                    }
                }
                result = null; return false;
            }
            result = this.termManager.Sub(left, right);
            return true;
        }

        private double EqualsDistance(Term left, Term right, bool isNegated, ref IArithmeticModel model)
        {
            if (isNegated)
                return NotEqualsDistance(left, right, ref model);

            Term diff;
            if (!TrySubtractTwoTerms(model.GetValue(left), model.GetValue(right), out diff))
            {
                return Double.MaxValue;
            }

            double distance;
            if (!TryConvertTermToDouble(model.GetValue(diff), out distance))
            {
                return Double.MaxValue;
            }

            distance = Math.Abs(distance);

            if (Double.IsInfinity(distance)
                    || Double.IsNaN(distance)
                    || Double.IsNegativeInfinity(distance)
                    || Double.IsPositiveInfinity(distance))
                return Double.MaxValue;
            else if (distance <= 0.0)
                return 0.0;
            else
                return distance;
        }
        private double NotEqualsDistance(Term left, Term right, ref IArithmeticModel model)
        {
            Term diff;
            if (!TrySubtractTwoTerms(model.GetValue(left), model.GetValue(right), out diff))
            {
                return Double.MaxValue;
            }

            double distance;
            if (!TryConvertTermToDouble(model.GetValue(diff), out distance))
            {
                return Double.MaxValue;
            }

            distance = Math.Abs(distance);

            if (Double.IsInfinity(distance)
                || Double.IsNaN(distance)
                || Double.IsNegativeInfinity(distance)
                || Double.IsPositiveInfinity(distance))
                return Double.MaxValue;
            else if (distance == 0.0)
                return this.K;
            else
                return 0.0;
        }

        private double LessThanDistance(Term left, Term right, bool isNegated, ref IArithmeticModel model)
        {
            if (isNegated)
                return GreaterThanOrEqualsDistance(left, right, ref model);

            Term diff;
            if (!TrySubtractTwoTerms(model.GetValue(left), model.GetValue(right), out diff))
            {
                return Double.MaxValue;
            }

            double distance;
            if (!TryConvertTermToDouble(model.GetValue(diff), out distance))
            {
                return Double.MaxValue;
            }

            if (Double.IsInfinity(distance)
                    || Double.IsNaN(distance)
                    || Double.IsNegativeInfinity(distance)
                    || Double.IsPositiveInfinity(distance))
                return Double.MaxValue;
            else if (distance < 0)
                return 0.0;
            else
                return (distance + this.K);
        }

        private double GreaterThanOrEqualsDistance(Term left, Term right, ref IArithmeticModel model)
        {
            Term diff;
            if (!TrySubtractTwoTerms(model.GetValue(right), model.GetValue(left), out diff))
            {
                return Double.MaxValue;
            }

            double distance;
            if (!TryConvertTermToDouble(model.GetValue(diff), out distance))
            {
                return Double.MaxValue;
            }

            if (Double.IsInfinity(distance)
                || Double.IsNaN(distance)
                || Double.IsNegativeInfinity(distance)
                || Double.IsPositiveInfinity(distance))
                return Double.MaxValue;
            else if (distance <= 0.0)
                return 0.0;
            else
                return (distance + this.K);
        }
        #endregion

        [__DoNotInstrument]
        protected static class Metadata
        {
            public static readonly TypeEx MathType = MetadataFromReflection.GetType(typeof(Math));
            public static readonly Method MathMin =
                MathType.GetMethod("Min", SystemTypes.Double, SystemTypes.Double);
            public static readonly Method MathLog =
                MathType.GetMethod("Log", SystemTypes.Double);
            public static readonly Method MathLogWithBase =
                MathType.GetMethod("Log", SystemTypes.Double, SystemTypes.Double);
            public static readonly Method MathPow =
                MathType.GetMethod("Pow", SystemTypes.Double, SystemTypes.Double);
            public static readonly Method MathFloatAbs =
                MathType.GetMethod("Abs", SystemTypes.Single);
            public static readonly Method MathDoubleAbs =
                MathType.GetMethod("Abs", SystemTypes.Double);
            public static readonly Method MathInt16Abs =
                MathType.GetMethod("Abs", SystemTypes.Int16);
            public static readonly Method MathInt32Abs =
                MathType.GetMethod("Abs", SystemTypes.Int32);
            public static readonly Method MathInt64Abs =
                MathType.GetMethod("Abs", SystemTypes.Int64);
            public static readonly Method MathCos =
                MathType.GetMethod("Cos", SystemTypes.Double);
            public static readonly Method MathSin =
                MathType.GetMethod("Sin", SystemTypes.Double);
            public static readonly Method MathTan =
                MathType.GetMethod("Tan", SystemTypes.Double);
            public static readonly Method MathACos =
                MathType.GetMethod("Acos", SystemTypes.Double);
            public static readonly Method MathASin =
                MathType.GetMethod("Asin", SystemTypes.Double);
            public static readonly Method MathATan =
                MathType.GetMethod("Atan", SystemTypes.Double);
            public static readonly Method MathCosh =
                MathType.GetMethod("Cosh", SystemTypes.Double);
            public static readonly Method MathSinh =
                MathType.GetMethod("Sinh", SystemTypes.Double);
            public static readonly Method MathTanh =
                MathType.GetMethod("Tanh", SystemTypes.Double);
            public static readonly Method MathSqrt =
                MathType.GetMethod("Sqrt", SystemTypes.Double);
        }

        #region IDisposable Members

        void IDisposable.Dispose()
        {
            this.modelBuilder = null;
        }

        #endregion
    }
}
