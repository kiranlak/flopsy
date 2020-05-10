using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PexWrapper
{
    class Wrapper
    {
        static int[] randomSeeds = new int[] {602023087,1636200043,440178153,879026598,684526821,
            1014828426,2078800969,1290778487,175584000,1100934013,2094594980,1819908996,1159050118,
            1992110428,482937059,159113257,984893103,672302827,2048152447,1882014091,124246834,1108762524,
            743863478,404105708,1173670333,1588637004,934644941,1399816934,811139316,30599556,1511062787,
            1789883706,1008243061,417665077,1659738818,597730677,1365834980,4015252,1195958853,2141136970,
            1731679318,211603960,873900087,25761304,912308044,267123290,1664371359,291261884,505296585,1867815555,
            1177548114,783128396,1219208587,351559402,1849007471,506739438,969998113,844283862,2052696930,2021803472};

        static int seedIndex;

        static ProcessStartInfo startInformation;

        private static void DisablePexDefaults(bool disableRandom)
        {
            AddEnvironmentVariable("pex_newton_enabled", "false");
            AddEnvironmentVariable("pex_arithmetic_avm_solver_disabled", "true");
            AddEnvironmentVariable("pex_arithmetic_avm_solver_fitness_budget", "0");
            if (disableRandom)
            {
                AddEnvironmentVariable("pex_arithmetic_random_solver_disabled", "true");
                AddEnvironmentVariable("pex_arithmetic_random_solver_attempts", "0");
            }
        }

        private static void SetGeneralParas(string fitnessBudget, string solver)
        {
            AddEnvironmentVariable("pex_custom_arithmetic_solver_evals", fitnessBudget);
            AddEnvironmentVariable("pex_custom_arithmetic_solver", solver);
        }
        
        private static void SetupES(string parents, string offspring, string recombination, string mutation, string poolSize)
        {
            AddEnvironmentVariable("es_solver_parents", parents);
            AddEnvironmentVariable("es_solver_offspring", offspring);
            AddEnvironmentVariable("es_solver_recomb", recombination);
            AddEnvironmentVariable("es_solver_mut", mutation);
            AddEnvironmentVariable("es_selection_pool", poolSize);
        }

        private static void AddEnvironmentVariable(string name, string value)
        {
            if (startInformation.EnvironmentVariables.ContainsKey(name))
            {
                startInformation.EnvironmentVariables.Remove(name);
            }

            startInformation.EnvironmentVariables.Add(name, value);
        }

        static string GetParameter(string arg)
        {
            string[] parts = arg.Split(':');
            if (parts.Length != 2)
                return null;
            else
                return parts[1];
        }

        static void Main(string[] args)
        {
            int repeats = 1;
            string targetDll = null;
            StringBuilder pexOptions = new StringBuilder();
            startInformation = new ProcessStartInfo("pex");

            string fitnessBudget = "100000";
            string solver = "AVM";
            bool disableRandom = true;
            bool pexDefault = false;

            #region ES
            string poolSize = "5";
            string parents = "15";
            string offspring = "100";
            string recomb = "GlobalDiscrete";
            string mutation = "Single";
            #endregion

            if (args == null || args.Length == 0)
            {
                throw new Exception("invalid arguments");
            }

            #region Command Line Arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].EndsWith(".dll"))
                {
                    targetDll = args[i];
                }
                else if (args[i].StartsWith("/repeats:"))
                {
                    repeats = Convert.ToInt32(GetParameter(args[i]));
                }
                else if (args[i].Equals("/default"))
                {
                    pexDefault = true;
                }
                else if (args[i].StartsWith("/fit:"))
                {
                    fitnessBudget = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/solver:"))
                {
                    solver = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/norand"))
                {
                    disableRandom = true;
                }
                else if (args[i].StartsWith("/pool:"))
                {
                    poolSize = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/parents:"))
                {
                    parents = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/offspring:"))
                {
                    offspring = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/recomb:"))
                {
                    recomb = GetParameter(args[i]);
                }
                else if (args[i].StartsWith("/mut:"))
                {
                    mutation = GetParameter(args[i]);
                }
                else
                {
                    pexOptions.Append(args[i] + " ");
                }
            }
            #endregion

            if (targetDll == null)
            {
                throw new Exception("no dll specified");
            }

            if (!pexDefault)
            {
                DisablePexDefaults(disableRandom);
                SetGeneralParas(fitnessBudget, solver);
                if (solver.Equals("ES"))
                    SetupES(parents, offspring, recomb, mutation, poolSize);
            }
            else
            {
                AddEnvironmentVariable("pex_custom_arithmetic_solver_evals", "0");
                AddEnvironmentVariable("pex_custom_arithmetic_solver", "");
                
                solver = "pex";
            }

            startInformation.CreateNoWindow = false;
            startInformation.UseShellExecute = false;
            startInformation.Arguments = targetDll + " " + pexOptions.ToString();

            for (int i = 0; i < repeats; i++)
            {
                seedIndex = i;

                AddEnvironmentVariable("er_random_seed", Convert.ToString(randomSeeds[seedIndex]));

                using (Process pex = Process.Start(startInformation))
                {
                    pex.WaitForExit();
                }
            }

            startInformation = null;
        }
    }
}
