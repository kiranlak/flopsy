// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Reasoning.ArithmeticSolving;
using Microsoft.ExtendedReflection.Monitoring;
using Microsoft.Pex.Engine;

namespace PexCustomArithmeticSolvers.Implementation
{
    [__DoNotInstrument]
    public sealed class PexAlternatingVariableHillClimbingArithmeticSolver
        : PexCustomArithmeticSolver
    {
        [__DoNotInstrument]
        private struct VariableInfo
        {
            public readonly IArithmeticVariable variable;
            public int precision;

            public VariableInfo(IArithmeticVariable v, int p)
            {
                this.variable = v;
                this.precision = p;
            }
        }

        private readonly SafeList<VariableInfo> variableInfos;

        private IModel bestModel;
        private IArithmeticModel currentModel;

        private double currentFitness;
        private double bestFitness;

        #region search parameters
        private short direction;
        private short lastDirection;
        private int index;
        private int lastIndex;
        private uint patternMoves;

        private bool increasedPrecision;
        private bool precisionChangeSuccess;
        private int precisionVariableIndex;
        #endregion

        public PexAlternatingVariableHillClimbingArithmeticSolver(
            string explorationName,
            bool isLoggingEnabled,
            bool isLoggingVerboseEnabled,
            int fitnessBudget)
            : base(explorationName, isLoggingEnabled, isLoggingVerboseEnabled, fitnessBudget, "AVM", null)
        {
            this.variableInfos = new SafeList<VariableInfo>();
            this.increasedPrecision = false;
            this.precisionChangeSuccess = false;
            this.precisionVariableIndex = 0;
            this.currentFitness = 0;
            this.bestFitness = Double.MaxValue;
        }

        public PexAlternatingVariableHillClimbingArithmeticSolver(
            string explorationName,
            bool isLoggingEnabled,
            bool isLoggingVerboseEnabled,
            int fitnessBudget,
            Method m)
            : base(explorationName, isLoggingEnabled, isLoggingVerboseEnabled, fitnessBudget, "AVM", m)
        {
            this.variableInfos = new SafeList<VariableInfo>();
            this.increasedPrecision = false;
            this.precisionChangeSuccess = false;
            this.precisionVariableIndex = 0;
            this.currentFitness = 0;
            this.bestFitness = Double.MaxValue;
        }

        #region AVM Main Members

        private bool EvaluateCurrentModel()
        {
            this.currentModel = base.modelBuilder.ToArithmeticModel();
            this.currentFitness = EvaluateArithmeticModel(ref this.currentModel);
            if (this.currentFitness < this.bestFitness)
            {
                UpdateBestModel();
                return true;
            }
            else
                return false;
        }

        private void UpdateBestModel()
        {
            if (IsLoggingVerboseEnabled)
            {
                base.context.Host.Log.LogVerbose(PexLogCategories.ArithmeticSolver, "updating best model found so far");
            }

            if (this.bestModel != base.context.InitialModel
                    && this.bestModel != null)
            {
                this.bestModel.Dispose();
                this.bestModel = null;
            }

            this.bestModel = base.modelBuilder.ToArithmeticModel();
            this.bestFitness = this.currentFitness;
        }

        private void ResetModelBuilder()
        {
            base.modelBuilder = base.context.CreateArithmeticModelBuilder(this.bestModel);
        }

        private TryGetModelResult StartSearch(out IArithmeticModel model)
        {
            TryGetModelResult result;
            bool foundImprovingMove = false;

            //SafeDebugger.Break();

            if (!TryBuildVariableVector())
            {
                model = null;
                return TryGetModelResult.NoModelFound;
            }

            this.bestFitness = 0;
            EvaluateCurrentModel();
            this.bestFitness = this.currentFitness;

            if (IsLoggingVerboseEnabled)
            {
                base.context.Host.Log.LogVerbose(PexLogCategories.ArithmeticSolver, "starting alternating variable method search");
            }

            bool randomizeLocal = true;

            while (!Terminate(out result, out model))
            {
                if (RequiresRestart())
                {
                    if (randomizeLocal)
                    {
                        if (IsLoggingVerboseEnabled)
                        {
                            base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "performing localized random restart after {0} fitness evaluations", base.fitnessEvaluations);
                        }

                        RandomizeFraction();

                        randomizeLocal = false;
                    }
                    else
                    {
                        if (IsLoggingVerboseEnabled)
                        {
                            base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "performing random restart after {0} fitness evaluations", base.fitnessEvaluations);
                        }

                        for (int i = 0; i < this.variableInfos.Count; i++)
                        {
                            var variableInfo = this.variableInfos[i];
                            var precision = variableInfo.precision;
                            if (precision != 0)
                                this.variableInfos[i] = new VariableInfo(this.variableInfos[i].variable, 0);
                        }

                        RandomizeInputVariablesBitConverter();
                        randomizeLocal = true;
                    }

                    ResetExplorationParameters(true);
                    this.bestFitness = 0;
                    EvaluateCurrentModel();
                    UpdateBestModel();
                    this.bestFitness = this.currentFitness;
                }
                else
                {
                    foundImprovingMove = ExploreNeighbourhood();

                    bool restartExploration = false;
                    bool terminateSearch = false;
                    while (foundImprovingMove && !(terminateSearch = Terminate(out result, out model)))
                    {
                        if (this.increasedPrecision)
                            this.precisionChangeSuccess = true;

                        foundImprovingMove = MakePatternMove();
                        restartExploration = true;
                    }

                    if (result == TryGetModelResult.Success || terminateSearch)
                        break;

                    if (restartExploration)
                        ResetExplorationParameters(true);

                    ResetModelBuilder();
                }
            }

            return result;
        }

        private bool TryBuildVariableVector()
        {
            if (IsLoggingVerboseEnabled)
            {
                base.context.Host.Log.LogVerbose(PexLogCategories.ArithmeticSolver, "building alternating variable vector");
            }

            this.variableInfos.Clear();

            foreach (var variable in base.context.Variables)
            {
                this.variableInfos.Add(new VariableInfo(variable, 0));
            }

            if (this.variableInfos.Count == 0)
            {
                if (IsLoggingVerboseEnabled)
                {
                    base.context.Host.Log.LogVerbose(PexLogCategories.ArithmeticSolver, "failed to build alternating variable vector - no suitable inputs");
                }
                return false;
            }
            return true;
        }

        private void RandomizeFraction()
        {
            Term randomValueTerm = null;
            
            for (int i = 0; i < this.variableInfos.Count; i++)
            {
                var variableInfo = this.variableInfos[i];
                var variable = variableInfo.variable;
                var precision = variableInfo.precision;
                var term = variable.Term;

                if (precision == 0)
                {
                    RandomizeInputVariableBitConverter(ref variable);
                    continue;
                }

                Term value = this.bestModel.GetValue(term);
                Layout layout = base.termManager.GetLayout(value);

                double rand = base.random.NextDouble();

                if (layout == Layout.R4)
                {
                    float R4value;
                    if (!base.termManager.TryGetR4Constant(value, out R4value))
                    {
                        RandomizeInputVariableBitConverter(ref variable);
                    }
                    else
                    {
                        R4value += (float)(Math.Pow(10, -precision) * rand);
                        randomValueTerm = base.termManager.R4(R4value);
                        base.modelBuilder.TryAssign(variableInfo.variable, randomValueTerm);
                    }
                }
                else if (layout == Layout.R8)
                {
                    double R8value;
                    if (!base.termManager.TryGetR8Constant(value, out R8value))
                    {
                        RandomizeInputVariableBitConverter(ref variable);
                    }
                    else
                    {
                        R8value += Math.Pow(10, -precision) * rand;
                        randomValueTerm = base.termManager.R8(R8value);
                        base.modelBuilder.TryAssign(variableInfo.variable, randomValueTerm);
                    }
                }
                else if (base.IsDecimalTerm(value))
                {
                    using (_ProtectingThreadContext.Acquire())
                    {
                        decimal decValue;
                        if (!base.termManager.TryGetDecimalConstant(value, out decValue))
                        {
                            RandomizeInputVariableBitConverter(ref variable);
                        }
                        else
                        {
                            decValue += Convert.ToDecimal(Math.Pow(10, -precision) * rand);
                            randomValueTerm = base.termManager.Decimal(decValue);
                            base.modelBuilder.TryAssign(variableInfo.variable, randomValueTerm);
                        }
                    }
                }
                else
                {
                    RandomizeInputVariableBitConverter(ref variable);
                }
            }
        }
        
        private int CountDecimalDigits(Term term, out int decimalPoint, out Layout layout)
        {
            decimalPoint = -1;
            String strvalue = null;
            layout = base.termManager.GetLayout(term);
            if (layout == Layout.R4)
            {
                float f;
                if (base.termManager.TryGetR4Constant(term, out f))
                {
                    strvalue = f.ToString("R");
                }
            }
            else if (layout == Layout.R8)
            {
                double d;
                if (base.termManager.TryGetR8Constant(term, out d))
                {
                    strvalue = d.ToString("R");
                }
            }
            else if (base.IsDecimalTerm(term))
            {
                decimal dec;
                if (base.termManager.TryGetDecimalConstant(term, out dec))
                {
                    strvalue = dec.ToString("D");
                }
            }
            if (strvalue != null)
            {
                decimalPoint = strvalue.IndexOf('.');
                if (decimalPoint != -1)
                    return strvalue.Length - 1;
                else
                    return strvalue.Length;
            }
            return 0;
        }
        
        private bool ExpandNeighbourhood(Term term, int precision)
        {
            Term value = this.bestModel.GetValue(term);
            Layout layout;
            int decimalPoint = -1;
            int digits = CountDecimalDigits(value, out decimalPoint, out layout);
            if (digits == 0)
                return false;
            if (decimalPoint == -1)
            {
                if (layout == Layout.R4)
                {
                    if (digits < 7 && precision < 7)
                        return true;
                }
                else if (layout == Layout.R8)
                {
                    if (digits < 15 && precision < 15)
                        return true;
                }
                else if (IsDecimalTerm(value))
                {
                    if (digits < 28 && precision < 28)
                        return true;
                }
            }
            else
            {
                if (layout == Layout.R4)
                {
                    if ((decimalPoint + 1) < 7 && (precision + decimalPoint) < 7)
                        return true;
                }
                else if (layout == Layout.R8)
                {
                    if ((decimalPoint + 1) < 15 && (precision + decimalPoint) < 15)
                        return true;
                }
                else if (IsDecimalTerm(value))
                {
                    if ((decimalPoint + 1) < 28 && (precision + decimalPoint) < 28)
                        return true;
                }
            }

            return false;
        }

        private bool TryChangePrecision(bool increase, ref int precision, Term term, Layout layout)
        {
            if (layout == Layout.R4)
            {
                if (!increase)
                {
                    precision = (precision > 0) ? (precision - 1) : 0;
                }
                else
                    precision++;
            }
            else if (layout == Layout.R8)
            {
                if (!increase)
                {
                    precision = (precision > 0) ? (precision - 1) : 0;
                }
                else
                    precision++;
            }
            else if (base.IsDecimalTerm(term))
            {
                if (!increase)
                {
                    precision = (precision > 0) ? (precision - 1) : 0;
                }
                else
                    precision++;
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool TryExpandNeighbourhood()
        {
            if (this.increasedPrecision && !this.precisionChangeSuccess)
            {
                this.precisionVariableIndex++;
            }

            this.increasedPrecision = false;
            this.precisionChangeSuccess = false;

            while (this.precisionVariableIndex < this.variableInfos.Count)
            {

                var variableInfo = this.variableInfos[this.precisionVariableIndex];
                int precision = variableInfo.precision;
                var term = variableInfo.variable.Term;

                var layout = base.termManager.GetLayout(term);
                if (ExpandNeighbourhood(term, precision))
                {
                    if (TryChangePrecision(true, ref precision, term, layout))
                    {
                        this.variableInfos[this.precisionVariableIndex] = new VariableInfo(variableInfo.variable, precision);
                        this.increasedPrecision = true;
                        return true;
                    }
                }
                this.precisionVariableIndex++;
            }
            this.precisionVariableIndex = 0;
            return false;
        }

        private bool RequiresRestart()
        {
            if (this.index >= this.variableInfos.Count)
            {
                if (TryExpandNeighbourhood())
                {
                    ResetExplorationParameters(false);
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool ExploreNeighbourhood()
        {
            this.lastDirection = this.direction;
            this.lastIndex = this.index;

            MakeNumericMove(false);

            if (this.direction < 0)
                this.direction = 1;
            else
            {
                this.direction = -1;
                this.index++;
            }

            return EvaluateCurrentModel();
        }

        private bool MakePatternMove()
        {
            this.patternMoves++;
            MakeNumericMove(true);
            return EvaluateCurrentModel();
        }

        private void MakeNumericMove(bool patternMove)
        {
            double delta = 0.0;
            short dir = this.direction;
            int index = this.index;

            if (patternMove)
            {
                dir = this.lastDirection;
                index = this.lastIndex;
            }

            SafeDebug.Assert(index < this.variableInfos.Count, "this.index < this.variableInfos.Count");

            var variableInfo = this.variableInfos[index];
            var currentVariable = variableInfo.variable;
            Term newValue = null;

            delta = (double)dir * Math.Pow(10, -variableInfo.precision) * Math.Pow(2, this.patternMoves);

            if (TryAddDoubleToTerm(currentVariable.Term, delta, out newValue))
            {
                base.modelBuilder.TryAssign(currentVariable, this.bestModel.GetValue(newValue));
            }
        }

        private void ResetExplorationParameters(bool full)
        {
            this.patternMoves = 0;
            this.direction = this.lastDirection = -1;
            this.index = this.lastIndex = 0;
            if (full)
            {
                this.increasedPrecision = this.precisionChangeSuccess = false;
                this.precisionVariableIndex = 0;
            }
        }

        #endregion

        private bool Terminate(out TryGetModelResult result, out IArithmeticModel model)
        {
            model = base.modelBuilder.ToArithmeticModel();
            if (base.context.IsValidModel(model))
            {
                if (IsLoggingEnabled)
                {
                    base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "AVM found solution after {0} fitness evaluations", base.fitnessEvaluations);
                    base.logManager.LogSuccess(base.fitnessEvaluations);
                }

                Dispose();

                result = TryGetModelResult.Success;
                return true;
            }
            model.Dispose();
            model = null;
            if (base.context.HasTimedOut)
            {
                if (IsLoggingEnabled)
                {
                    base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "AVM timed out after {0} unsuccessful fitness evaluations", base.fitnessEvaluations);
                    base.logManager.LogFailure();
                }
                Dispose();
                result = TryGetModelResult.Timeout;
                return true;
            }
            else if (base.fitnessEvaluations >= base.fitnessBudget)
            {
                if (IsLoggingEnabled)
                {
                    base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "AVM failed to find a solution after {0} fitness evaluations", base.fitnessEvaluations);
                    base.logManager.LogFailure();
                }
                Dispose();
                result = TryGetModelResult.NoModelFound;
                return true;
            }
            else
            {
                result = TryGetModelResult.NoModelFound;
                return false;
            }
        }

        public override TryGetModelResult TryGetArithmeticModel(IArithmeticSolvingContext context, out IArithmeticModel model)
        {
            InitializeCustomSolver(context);

            this.bestModel = context.InitialModel;

            ResetExplorationParameters(true);

            TryGetModelResult result = StartSearch(out model);

            return result;
        }

        #region Disposable Members

        private void Dispose()
        {
            if (this.bestModel != null &&
                this.bestModel != base.context.InitialModel)
            {
                this.bestModel.Dispose();
                this.bestModel = null;
            }
            if (this.currentModel != null &&
                this.currentModel != base.context.InitialModel)
            {
                this.currentModel.Dispose();
                this.currentModel = null;
            }
            base.modelBuilder = null;
        }

        #endregion
    }
}