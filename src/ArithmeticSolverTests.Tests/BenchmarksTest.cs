// <copyright file="BenchmarksTest.cs" company="Microsoft">Copyright © Microsoft 2010</copyright>
using System;
using ArithmeticSolverTests;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArithmeticSolverTests
{
    /// <summary>This class contains parameterized unit tests for Benchmarks</summary>
    [PexClass(typeof(Benchmarks))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAssertReachEventually]
    [TestClass]
    public partial class BenchmarksTest
    {
        /// <summary>Test stub for Beale(Double, Double)</summary>
        [PexMethod]
        public void Beale(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2
        )
        {
            target.Beale(x1, x2);
            // TODO: add assertions to method BenchmarksTest.Beale(Benchmarks, Double, Double)
        }

        /// <summary>Test stub for FreudensteinAndRoth(Double, Double)</summary>
        [PexMethod]
        public void FreudensteinAndRoth(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2
        )
        {
            target.FreudensteinAndRoth(x1, x2);
            // TODO: add assertions to method BenchmarksTest.FreudensteinAndRoth(Benchmarks, Double, Double)
        }

        /// <summary>Test stub for HelicalValley(Double, Double, Double)</summary>
        [PexMethod]
        public void HelicalValley(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2,
            double x3
        )
        {
            target.HelicalValley(x1, x2, x3);
            // TODO: add assertions to method BenchmarksTest.HelicalValley(Benchmarks, Double, Double, Double)
        }

        /// <summary>Test stub for Powell(Double, Double)</summary>
        [PexMethod]
        public void Powell(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2
        )
        {
            target.Powell(x1, x2);
            // TODO: add assertions to method BenchmarksTest.Powell(Benchmarks, Double, Double)
        }

        /// <summary>Test stub for Rosenbrock(Double, Double)</summary>
        [PexMethod]
        public void Rosenbrock(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2
        )
        {
            target.Rosenbrock(x1, x2);
            // TODO: add assertions to method BenchmarksTest.Rosenbrock(Benchmarks, Double, Double)
        }

        /// <summary>Test stub for WoodFunction(Double, Double, Double, Double)</summary>
        [PexMethod]
        public void WoodFunction(
            [PexAssumeUnderTest]Benchmarks target,
            double x1,
            double x2,
            double x3,
            double x4
        )
        {
            target.WoodFunction(x1, x2, x3, x4);
            // TODO: add assertions to method BenchmarksTest.WoodFunction(Benchmarks, Double, Double, Double, Double)
        }
    }
}
