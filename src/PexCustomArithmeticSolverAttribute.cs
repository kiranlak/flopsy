using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Framework.Strategies;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Reasoning.ArithmeticSolving;
using PexCustomArithmeticSolvers.Implementation;
using Microsoft.Pex.Engine;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using System.Security;

namespace PexCustomArithmeticSolvers
{
    [__DoNotInstrument]
    public sealed class PexCustomArithmeticSolverAttribute
         : PexArithmeticSolverAttributeBase
    {
        private string currentExploration;

        private string GetEnvironmentVariable(string environmentVariable, string @default)
        {
            try
            {
                return System.Environment.GetEnvironmentVariable(environmentVariable);
            }
            catch (System.ArgumentNullException)
            {
                //Console.WriteLine("GetEnvironmentVariable Exception:" + e.Message);
            }
            catch (SecurityException)
            {
                //Console.WriteLine("GetEnvironmentVariable Exception:" + e.Message);
            }
            return @default;
        }

        protected override bool TryCreateArithmeticSolver(
            IPexExplorationComponent host,
            out IArithmeticSolver solver)
        {
            int fitnessEvals = Convert.ToInt32(GetEnvironmentVariable("pex_custom_arithmetic_solver_evals", "100000"));
            string customSolver = GetEnvironmentVariable("pex_custom_arithmetic_solver", "ES");

            currentExploration = host.ExplorationServices.CurrentExploration.Exploration.Method.FullName;

            if (customSolver != null && customSolver.Equals("AVM"))
            {
                solver = new PexAlternatingVariableHillClimbingArithmeticSolver(
                    currentExploration,
                    true,
                    false,
                    fitnessEvals,
                    host.ExplorationServices.CurrentExploration.Exploration.Method);
            }
            else if (customSolver != null && customSolver.Equals("ES"))
            {
                int parents = Convert.ToInt32(GetEnvironmentVariable("es_solver_parents", "15"));
                int offspring = Convert.ToInt32(GetEnvironmentVariable("es_solver_offspring", "100"));
                PexEvolutionStrategyArithmeticSolver.RecombinationStrategy recombination;
                PexEvolutionStrategyArithmeticSolver.MutationStrategy mutation;
                switch (GetEnvironmentVariable("es_solver_recomb", "GlobalDiscrete"))
                {
                    case "Discrete":
                        recombination = PexEvolutionStrategyArithmeticSolver.RecombinationStrategy.Discrete;
                        break;
                    case "GlobalDiscrete":
                        recombination = PexEvolutionStrategyArithmeticSolver.RecombinationStrategy.GlobalDiscrete;
                        break;
                    case "GlobalIntermediate":
                        recombination = PexEvolutionStrategyArithmeticSolver.RecombinationStrategy.GlobalIntermediate;
                        break;
                    case "Intermediate":
                        recombination = PexEvolutionStrategyArithmeticSolver.RecombinationStrategy.Intermediate;
                        break;
                    case "None":
                    default: recombination = PexEvolutionStrategyArithmeticSolver.RecombinationStrategy.None; break;
                }
                switch (GetEnvironmentVariable("es_solver_mut", "Single"))
                {
                    case "Multi":
                        mutation = PexEvolutionStrategyArithmeticSolver.MutationStrategy.Multi;
                        break;
                    case "Single":
                        mutation = PexEvolutionStrategyArithmeticSolver.MutationStrategy.Single;
                        break;
                    case "None":
                    default: mutation = PexEvolutionStrategyArithmeticSolver.MutationStrategy.None; break;
                }
                solver = new PexEvolutionStrategyArithmeticSolver(
                    currentExploration,
                    true,
                    false,
                    fitnessEvals,
                    parents,
                    offspring,
                    recombination,
                    mutation,
                    false,
                    host.ExplorationServices.CurrentExploration.Exploration.Method);
            }
            else
            {
                host.Log.LogMessage(PexLogCategories.ArithmeticSolver, "pex_custom_arithmetic_solver set to wrong value");
                solver = null;
                return false;
            }
            return true;
        }
    }
}
