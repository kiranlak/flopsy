
**Project Description**  
Pex Custom Arithmetic Solver contains a collection of meta-heuristic search algorithms. The goal is to improve Pex's code coverage for code involving floating point variables. The project is based on the PexArithmeticSolverAttributeBase.  

The project has been evaluated in a paper entitled:  

_FloPSy - Search-Based Floating Point Constraint Solving for Symbolic Execution.  
K. Lakhotia, N. Tillmann, M. Harman, J. de Halleux, 22nd IFIP International Conference on Testing Software and Systems, Natal, Brazil, November 8-12, 2010, pp. 142-157._  

During its exploration, Pex constructs path conditions describing a succinct path through your program. The path condition is made up of conjuncts involving constraints over input variables (to your program). When the [PexCustomArithmeticSolver] attribute is set for one of your methods or class, Pex collects all variables (of type float, double or decimal) which appear in conjuncts from the path condition that cannot be handled by its constraint solver, and passes them to this extension class. The PexCustomArithmeticSolver then uses a meta-heuristic search algorithm to try and find values for these input variables which satisfy the path condition. The search is guided by a fitness function which is a measure of "how close" we are to satisfying the path condition. It is based on the standard "branch distance" functions used in Search-Based Testing [1]. McMinn [2] provides an excellent survey on search-based testing.  

The project currently contains two solvers:  

AVM - Alternating Variable Method. This method is a form of hill climbing and was developed by Korel [1]. It is a simple search technique, which was shown to be very e ffective by Harman and McMinn [3] when compared with more sophisticated optimisation techniques such as genetic algorithms. The AVM first constructs a vector of input variables; those variables for which Pex could not find a value to satisfy the current path condition. The AVM then explores the "neighbourhood" of each input variable in this vector in turn. If changes in the values of the input variable do not result in an increased fitness, the search considers the next input variable, and so on - recommencing from the first input variable if necessary - until no further improvements can be made or test data has been found.  

ES - Evolution Strategies. This is an optimisation technique belonging to the family of evolutionary algorithms. It can be used as a hill climber (1+1)-ES, or a population based algorithm. In the former, an offspring is constructed from a randomly generated parent via mutation. If the offspring is fitter than its parent, it becomes the new parent and the process repeats. When used as a population based algorithm, one can use recombination operators as well as mutation operators to generate offspring. This solver implements a number of different recombination and mutation strategies for you to choose from. The page [http://www.scholarpedia.org/article/Evolution_strategies](http://www.scholarpedia.org/article/Evolution_strategies) provides and introduction to ES.  

**Usage**  
To use the PexCustomArithmeticSolvers extension you need to reference the project from within your own project. The attribute [PexCustomArithmeticSolver] also needs to be set, either at the class or method level. The project has a folder "Test", which contains some sample functions you can try out.  

**Environment Variables**  
pex_custom_arithmetic_solver - specifies the solver to use, i.e. AVM or ES  
pex_custom_arithmetic_solver_evals - sets the maximum number of fitness evaluations the solver is allowed to use  

es_solver_parents - specifies the size of the parent population  
es_selection_pool - specifies how many parents to select for recombination  
es_solver_offspring - specifies how many offspring to generate  
es_solver_recomb - specifies the recombination strategy. Choose between Discrete, GlobalDiscrete, Intermediate, GlobalIntermediate and None.  
es_solver_mut - specifies the mutation strategy. Choose between Multi, Single and None  

**References**  
[1] - Bogdan Korel. Automated software test data generation. IEEE Transactions on Software Engineering, 16(8):870-879, 1990  
[2] - Phil McMinn. Search-based test data generation: A survey. Software Testing, Verification and Reliability, 14(2):105-156, June 2004  
[3] - Mark Harman and Phil McMinn. A theoretical and empirical analysis of evolutionary testing and hill climbing for structural test data generation. ACM International Symposium on Software Testing and Analysis, pages 73-83, 2007
