using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Reasoning.ArithmeticSolving;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using System.Collections;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.Pex.Engine;
using Microsoft.ExtendedReflection.Metadata;

namespace PexCustomArithmeticSolvers.Implementation
{
    [__DoNotInstrument]
    public sealed class PexEvolutionStrategyArithmeticSolver
        : PexCustomArithmeticSolver,
          IDisposable
    {
        public enum RecombinationStrategy
        {
            None,
            Discrete,
            Intermediate,
            GlobalDiscrete,
            GlobalIntermediate
        }

        public enum MutationStrategy
        {
            None,
            Single,
            Multi
        }

        private readonly int populationSize;
        private readonly int offspringPopulationSize;
        private ESSolution[] parentPopulation;
        private SafeList<ESSolution> intermediatePopulation;

        private int variableCount;

        private RecombinationStrategy recombination;
        private MutationStrategy mutation;
        private bool selectFromOffspringOnly;

        public PexEvolutionStrategyArithmeticSolver(
            string explorationName,
            bool isLoggingEnabled,
            bool isLoggingVerboseEnabled,
            int fitnessBudget,
            int populationSize,
            int offspringPopulationSize,
            RecombinationStrategy recombination,
            MutationStrategy mutation,
            bool selectFromOffspringOnly)
            : base(explorationName, isLoggingEnabled, isLoggingVerboseEnabled, fitnessBudget, "ES", null)
        {
            this.variableCount = 0;
            this.populationSize = populationSize;
            this.offspringPopulationSize = offspringPopulationSize;
            this.parentPopulation = new ESSolution[this.populationSize];
            this.intermediatePopulation = new SafeList<ESSolution>();
            this.selectFromOffspringOnly = selectFromOffspringOnly;
            this.recombination = recombination;
            this.mutation = mutation;
        }

        public PexEvolutionStrategyArithmeticSolver(
            string explorationName,
            bool isLoggingEnabled,
            bool isLoggingVerboseEnabled,
            int fitnessBudget,
            int populationSize,
            int offspringPopulationSize,
            RecombinationStrategy recombination,
            MutationStrategy mutation,
            bool selectFromOffspringOnly,
            Method m)
            : base(explorationName, isLoggingEnabled, isLoggingVerboseEnabled, fitnessBudget, "ES", m)
        {
            this.variableCount = 0;
            this.populationSize = populationSize;
            this.offspringPopulationSize = offspringPopulationSize;
            this.parentPopulation = new ESSolution[this.populationSize];
            this.intermediatePopulation = new SafeList<ESSolution>();
            this.selectFromOffspringOnly = selectFromOffspringOnly;
            this.recombination = recombination;
            this.mutation = mutation;
        }

        private ESSolution CreateRandomSolution()
        {
            RandomizeInputVariablesWithBounds(-100, 100);
            ESSolution candidate = new ESSolution(this, base.modelBuilder.ToArithmeticModel(), this.variableCount);
            candidate.Initialize();
            return candidate;
        }

        private void CreateInitialPopulation()
        {
            base.modelBuilder = base.context.CreateArithmeticModelBuilder(base.context.InitialModel);

            ClearIntermediatePopulation();

            for (int i = 0; i < this.populationSize; i++)
            {
                this.intermediatePopulation.Add(CreateRandomSolution());
            }
        }

        private void ClearIntermediatePopulation()
        {
            for (int i = 0; i < this.intermediatePopulation.Count; i++)
            {
                this.intermediatePopulation[i].Dispose();
            }
            this.intermediatePopulation.Clear();
        }

        private bool Terminate(out TryGetModelResult result, out IArithmeticModel model)
        {
            for (int i = 0; i < this.populationSize; i++)
            {
                if (base.context.IsValidModel(this.parentPopulation[i].currentModel))
                {
                    if (IsLoggingEnabled)
                    {
                        base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "es method found solution after {0} fitness evaluations", base.fitnessEvaluations);
                        base.logManager.LogSuccess(base.fitnessEvaluations);
                    }
                    base.modelBuilder = base.context.CreateArithmeticModelBuilder(this.parentPopulation[i].currentModel);
                    model = base.modelBuilder.ToArithmeticModel();
                    result = TryGetModelResult.Success;
                    Dispose();
                    return true;
                }
            }

            if (base.context.HasTimedOut)
            {
                if (IsLoggingEnabled)
                {
                    base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "es method timed out after {0} unsuccessful fitness evaluations", base.fitnessEvaluations);
                    base.logManager.LogFailure();
                }
                result = TryGetModelResult.Timeout;
                model = null;
                Dispose();
                return true;
            }
            else
            {
                result = TryGetModelResult.NoModelFound;
                model = null;

                if (base.fitnessEvaluations >= base.fitnessBudget)
                {
                    if (IsLoggingEnabled)
                    {
                        base.context.Host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "es method failed to find a solution after {0} fitness evaluations", base.fitnessEvaluations);
                        base.logManager.LogFailure();
                    }
                    Dispose();
                    return true;
                }

                return false;
            }
        }

        private void GenerateTwoIndeces(out int index1, out int index2)
        {
            index1 = base.random.Next(0, this.parentPopulation.Length);
            index2 = index1;

            while (index2 == index1)
            {
                index2 = base.random.Next(0, this.parentPopulation.Length);
            }
        }

        #region Recombination
        private void Recombine(ref ESSolution offspring)
        {
            switch (this.recombination)
            {
                case RecombinationStrategy.None:
                    break;
                case RecombinationStrategy.Discrete:
                    DiscreteRecombination(ref offspring);
                    break;
                case RecombinationStrategy.GlobalDiscrete:
                    GlobalDiscreteRecombination(ref offspring);
                    break;
                case RecombinationStrategy.Intermediate:
                    IntermediateRecombination(ref offspring);
                    break;
                case RecombinationStrategy.GlobalIntermediate:
                    GlobalIntermediateRecombination(ref offspring);
                    break;
                default: break;
            }
        }
        private void DiscreteRecombination(ref ESSolution offspring)
        {
            int index1, index2;
            int index = 0;
            GenerateTwoIndeces(out index1, out index2);

            ESSolution parent1 = this.parentPopulation[index1];
            ESSolution parent2 = this.parentPopulation[index2];

            offspring = parent1.MakeDeepCopy();

            foreach (var variable in base.context.Variables)
            {
                if (base.random.Next(0, 2) != 0)
                {
                    base.modelBuilder.TryAssign(variable, parent2.currentModel.GetValue(variable.Term));
                    offspring.SetStdDev(index, parent2.GetStdDev(index));
                }
                index++;
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        private void GlobalDiscreteRecombination(ref ESSolution offspring)
        {
            int index1, index2;
            ESSolution parent1 = null;
            ESSolution parent2 = null;
            int index = 0;

            offspring = new ESSolution(this, this.variableCount);

            foreach (var variable in base.context.Variables)
            {
                GenerateTwoIndeces(out index1, out index2);
                parent1 = this.parentPopulation[index1];
                parent2 = this.parentPopulation[index2];

                if (base.random.Next(0, 2) == 0)
                {
                    base.modelBuilder.TryAssign(variable, parent1.currentModel.GetValue(variable.Term));
                    offspring.SetStdDev(index, parent1.GetStdDev(index));
                }
                else
                {
                    base.modelBuilder.TryAssign(variable, parent2.currentModel.GetValue(variable.Term));
                    offspring.SetStdDev(index, parent2.GetStdDev(index));
                }

                index++;
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        private Term DivideByTwo(Term t)
        {
            LayoutKind kind = base.termManager.GetLayoutKind(t);
            Term two = base.termManager.Add(base.termManager.One(kind), base.termManager.One(kind));
            return base.termManager.Div(t, two);
        }
        private void IntermediateRecombination(ref ESSolution offspring)
        {
            int index1, index2;

            GenerateTwoIndeces(out index1, out index2);

            ESSolution parent1 = this.parentPopulation[index1];
            ESSolution parent2 = this.parentPopulation[index2];

            offspring = new ESSolution(this, this.variableCount);

            Term sum = null;
            int index = 0;

            foreach (var variable in base.context.Variables)
            {
                sum = base.termManager.Add(parent1.currentModel.GetValue(variable.Term),
                    parent2.currentModel.GetValue(variable.Term));

                base.modelBuilder.TryAssign(variable, DivideByTwo(sum));
                double stdDev1 = parent1.GetStdDev(index);
                double stdDev2 = parent2.GetStdDev(index);
                offspring.SetStdDev(index, (0.5 * (stdDev1 + stdDev2)));

                index++;
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        private void GlobalIntermediateRecombination(ref ESSolution offspring)
        {
            int index1, index2;

            ESSolution parent1 = null;
            ESSolution parent2 = null;

            offspring = new ESSolution(this, this.variableCount);

            Term sum = null;
            int index = 0;

            foreach (var variable in base.context.Variables)
            {
                GenerateTwoIndeces(out index1, out index2);
                parent1 = this.parentPopulation[index1];
                parent2 = this.parentPopulation[index2];

                sum = base.termManager.Add(parent1.currentModel.GetValue(variable.Term),
                    parent2.currentModel.GetValue(variable.Term));

                base.modelBuilder.TryAssign(variable, DivideByTwo(sum));

                double stdDev1 = parent1.GetStdDev(index);
                double stdDev2 = parent2.GetStdDev(index);
                offspring.SetStdDev(index, (0.5 * (stdDev1 + stdDev2)));

                index++;
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        #endregion

        #region Mutation
        private void Mutate(ref ESSolution offspring)
        {
            switch (this.mutation)
            {
                case MutationStrategy.Multi:
                    MultiStdDevMutation(ref offspring);
                    break;
                case MutationStrategy.Single:
                    SingleStdDevMutation(ref offspring);
                    break;
                default: break;
            }
        }
        private void SingleStdDevMutation(ref ESSolution offspring)
        {
            SafeDebug.AssertNotNull(offspring, "offspring != null");

            base.modelBuilder = base.context.CreateArithmeticModelBuilder(offspring.currentModel);

            double learningRate = (double)1 / Math.Sqrt((double)base.context.Variables.Count<IArithmeticVariable>());

            double stdDev = offspring.GetStdDev(0) * Math.Exp(learningRate * Gaussian(0, 1));
            offspring.SetStdDev(0, stdDev);
            foreach (var variable in base.context.Variables)
            {
                Term sum = null;
                bool success = TryAddDoubleToTerm(variable.Term, stdDev * Gaussian(0, 1), out sum);
                if (success)
                {
                    base.modelBuilder.TryAssign(variable, offspring.currentModel.GetValue(sum));
                }
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        private void MultiStdDevMutation(ref ESSolution offspring)
        {
            SafeDebug.AssertNotNull(offspring, "offspring != null");

            base.modelBuilder = base.context.CreateArithmeticModelBuilder(offspring.currentModel);

            double globalLearningRate = (double)1 / Math.Sqrt((double)2 * (double)base.context.Variables.Count<IArithmeticVariable>());
            double localLearningRate = (double)1 / Math.Sqrt((double)2 * Math.Sqrt((double)base.context.Variables.Count<IArithmeticVariable>()));
            double overallT = globalLearningRate * Gaussian(0, 1);
            int index = 0;
            foreach (var variable in base.context.Variables)
            {
                Term sum = null;
                double ni = overallT + localLearningRate * Gaussian(0, 1);
                double scaleFactor = offspring.GetStdDev(index) * Math.Exp(ni);
                offspring.SetStdDev(index, scaleFactor);
                bool success = TryAddDoubleToTerm(variable.Term, scaleFactor * Gaussian(0, 1), out sum);
                if (success)
                {
                    base.modelBuilder.TryAssign(variable, offspring.currentModel.GetValue(sum));
                }
                index++;
            }

            offspring.UpdateModel(base.modelBuilder.ToArithmeticModel());
        }
        #endregion

        private TryGetModelResult Evolve(out IArithmeticModel model)
        {
            TryGetModelResult result = TryGetModelResult.None;

            //SafeDebugger.Break();

            CreateInitialPopulation();

            EvaluateIntermediatePopulation();

            for (int i = 0; i < this.populationSize; i++)
            {
                this.parentPopulation[i] = this.intermediatePopulation[i].MakeDeepCopy();
            }

            while (!Terminate(out result, out model))
            {
                ClearIntermediatePopulation();

                if (this.populationSize > 1 && this.recombination != RecombinationStrategy.None
                    && this.parentPopulation.Length > 1)
                {
                    ESSolution offspring = null;

                    for (int i = 0; i < this.offspringPopulationSize; i++)
                    {
                        Recombine(ref offspring);

                        if (offspring != null)
                        {
                            Mutate(ref offspring);

                            this.intermediatePopulation.Add(offspring.MakeDeepCopy());
                            offspring.Dispose();
                            offspring = null;
                        }
                    }
                }
                else
                {
                    ESSolution offspring = null;
                    int poolIndex = 0;
                    for (int i = 0; i < this.offspringPopulationSize; i++)
                    {
                        if (poolIndex >= this.parentPopulation.Length)
                            poolIndex = 0;

                        offspring = this.parentPopulation[poolIndex].MakeDeepCopy();
                        Mutate(ref offspring);
                        this.intermediatePopulation.Add(offspring.MakeDeepCopy());
                        offspring.Dispose();
                        offspring = null;
                        poolIndex++;
                    }
                }

                EvaluateIntermediatePopulation();

                //selection
                if (!this.selectFromOffspringOnly)
                {
                    for (int i = 0; i < this.populationSize; i++)
                    {
                        this.intermediatePopulation.Add(this.parentPopulation[i].MakeDeepCopy());
                    }
                }

                this.intermediatePopulation.Sort();

                for (int i = 0; i < this.populationSize && i < this.intermediatePopulation.Count; i++)
                {
                    this.parentPopulation[i].Dispose();
                    this.parentPopulation[i] = this.intermediatePopulation[i].MakeDeepCopy();
                }
            }

            return result;
        }

        private void EvaluateIntermediatePopulation()
        {
            for (int i = 0; i < this.intermediatePopulation.Count; i++)
            {
                this.intermediatePopulation[i].Fitness = EvaluateArithmeticModel(ref this.intermediatePopulation[i].currentModel);
            }
        }

        public override TryGetModelResult TryGetArithmeticModel(IArithmeticSolvingContext context, out IArithmeticModel model)
        {
            InitializeCustomSolver(context);

            this.variableCount = context.Variables.ToArray<IArithmeticVariable>().Length;

            model = null;

            TryGetModelResult result = (this.variableCount == 0)? TryGetModelResult.NoModelFound:Evolve(out model);

            return result;
        }

        #region Disposable Members

        private void Dispose()
        {
            for (int i = 0; i < this.populationSize; i++)
            {
                this.parentPopulation[i].Dispose();
                this.parentPopulation[i] = null;
            }

            for (int i = 0; i < this.intermediatePopulation.Count; i++)
            {
                this.intermediatePopulation[i].Dispose();
            }
            this.intermediatePopulation.Clear();
        }

        #endregion

        #region ES-class
        [__DoNotInstrument]
        class ESSolution
            : IComparable
        {
            private readonly PexEvolutionStrategyArithmeticSolver solver;
            private double[] stdDeviations;

            public IArithmeticModel currentModel;

            private double fitness;
            public double Fitness
            {
                get { return this.fitness; }
                set { this.fitness = value; }
            }

            public double GetStdDev(int index)
            {
                return this.stdDeviations[index];
            }
            public void SetStdDev(int index, double value)
            {
                this.stdDeviations[index] = Math.Abs(value);
            }

            public ESSolution(PexEvolutionStrategyArithmeticSolver solver, int variableCount)
            {
                this.solver = solver;
                this.currentModel = null;
                this.stdDeviations = new double[variableCount];
                this.fitness = Double.MaxValue;
            }
            public ESSolution(PexEvolutionStrategyArithmeticSolver solver, IArithmeticModel model, int variableCount)
            {
                this.solver = solver;
                this.currentModel = model;
                this.stdDeviations = new double[variableCount];
                this.fitness = Double.MaxValue;
            }
            public ESSolution(PexEvolutionStrategyArithmeticSolver solver, IArithmeticModel model, double[] stdDeviations, double fitness)
            {
                this.solver = solver;
                this.currentModel = model;
                this.stdDeviations = new double[stdDeviations.Length];
                stdDeviations.CopyTo(this.stdDeviations, 0);
                this.fitness = fitness;
            }

            public void Initialize()
            {
                for (int i = 0; i < stdDeviations.Length; i++)
                {
                    this.stdDeviations[i] = 1;// Math.Abs(this.solver.Gaussian());
                }
            }

            public void UpdateModel(IArithmeticModel model)
            {
                if (this.currentModel != null)
                {
                    this.currentModel.Dispose();
                    this.currentModel = null;
                }
                this.currentModel = model;
            }

            public ESSolution MakeDeepCopy()
            {
                IArithmeticModelBuilder mb = solver.context.CreateArithmeticModelBuilder(this.currentModel);
                ESSolution clone = new ESSolution(
                    this.solver,
                    mb.ToArithmeticModel(),
                    this.stdDeviations,
                    this.fitness);
                mb = null;
                return clone;
            }

            #region Disposable members

            public void Dispose()
            {
                if (this.currentModel != null)
                {
                    this.currentModel.Dispose();
                    this.currentModel = null;
                }
            }

            #endregion

            #region IComparable Members

            public int CompareTo(object obj)
            {
                ESSolution s = (ESSolution)obj;
                if (this.fitness > s.fitness)
                    return 1;
                else if (this.fitness < s.fitness)
                    return -1;
                else
                    return 0;
            }

            #endregion
        }
        #endregion
    }
}
