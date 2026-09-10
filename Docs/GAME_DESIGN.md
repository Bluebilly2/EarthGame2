# MASTER PROJECT CONTEXT AND ARCHITECTURAL DIRECTIVE

> Provenance: written by the project owner (William, 2026-08-24). This is the authoritative
> high-level design brief, philosophical foundation, and architectural direction for the project.
> It is NOT a request to implement everything immediately: the game is developed through small,
> testable vertical slices that progressively prove the underlying architecture. When making
> technical decisions, prefer solutions that preserve the principles below even if the first
> implementation is simplified.
>
> Note for EarthGame2 (2026-09-10): the planet-walker prototype this note once described was v1's
> (`Docs/v1/ARCHITECTURE.md`). EarthGame2's architecture is `Docs/ARCHITECTURE.md`, and the dated amendments
> to this constitution (§45's first slice deferred; §2's Earth begun as one bounded real region) are in
> `Docs/CANON.md`.

---

# 1. THE CORE GAME CONCEPT

The game is a playable thought experiment:

> **What happens if a single naked, unmodified human appears on an otherwise completely natural present-day Earth where humanity never evolved, but possesses access to the knowledge necessary to reconstruct technological civilization?**

The human begins with:

* no clothing
* no tools
* no weapons
* no shelter
* no manufactured materials
* no infrastructure
* no civilization
* no other humans

They have only their body, the natural environment, and whatever knowledge/information the game's knowledge system provides.

The player is therefore not merely surviving in a primitive world.
They are effectively trying to **become civilization**.

The long-term progression is:

> survival → tools → materials → manufacturing → machines → power → automation → industry → civilization → planetary infrastructure → space infrastructure → increasingly large-scale civilization

Potentially:

> Earth → orbit → Moon → Mars → asteroid industry → planetary-scale industry → stellar-scale infrastructure → Kardashev Type I → Type II → Type III → potentially beyond

There is intentionally no predetermined final technological endpoint at the current stage of development.

The game should be architected so that its **simulation capability**, rather than a prewritten storyline, determines how far the player can ultimately progress.

The current project can stop at a particular technology level while remaining explicitly **WIP**. The architecture should not assume that level is the true end of civilization.

---

# 2. THE WORLD IS REAL EARTH, NOT A GENERIC EARTH-LIKE WORLD

The game world should correspond as closely as practical to the real world under the counterfactual assumption that humans never existed.

The intent is not "a procedurally generated planet vaguely resembling Earth."

The intent is:

> **present-day Earth minus humanity and everything humanity has changed.**

The following should therefore correspond to reality as closely as feasible:

* Earth's size
* mass
* gravity
* rotation
* orbital parameters
* atmosphere
* oceans
* continents
* coastlines
* mountains
* major geological structures
* rivers
* lakes
* deserts
* climate zones
* biomes
* natural resource distribution
* the Moon
* the Sun
* Mars
* planetary distances and orbital relationships
* the broader Solar System
* real-world flora
* real-world fauna

The Earth should represent a world with **no human intervention**.

Therefore there should be no:

* cities
* roads
* farms
* artificial structures
* mines
* dams
* power lines
* industrial pollution
* domesticated animals
* cultivated crops
* human-caused extinctions
* satellites
* radio transmissions
* nuclear contamination
* artificially altered ecosystems

The flora and fauna should reflect the natural world that would exist under this premise.

The game is therefore fundamentally different from a post-apocalyptic game.
This is **pre-human Earth**, not ruined human Earth.

---

# 3. REAL-WORLD KNOWLEDGE MUST TRANSFER INTO THE GAME

This is one of the most important design goals.

The game should not merely *look* realistic.
It should be sufficiently causally faithful that real-world expertise becomes useful.

A player who knows wilderness survival should be able to apply that knowledge.

For example:

If a knowledgeable hiker knows that particular geological formations, drainage channels, valleys, springs, snowmelt zones, etc. are promising places to find fresh water, the game should reproduce those relationships.

If a metallurgist understands:

* ore
* phase behaviour
* furnace conditions
* heat treatment
* cooling rate
* alloy composition
* defects

that knowledge should materially help them.

If an electrical engineer understands:

* voltage
* current
* resistance
* power
* inductance
* generators
* motors
* etc.

that knowledge should materially help them.

If an aerospace engineer understands:

* propulsion
* structural margins
* orbital mechanics
* mass ratio
* guidance
* thermal constraints

that knowledge should materially help them.

If a programmer understands computing, algorithms and information systems, that knowledge should eventually help them.

The game should therefore not simplify difficult subjects simply to make them more accessible.

---

# 4. THE GAME IS NOT INTENDED TO BE FORGIVING

This is an explicit design philosophy.

Do not artificially make difficult real-world fields easy.

If something is difficult in reality because the underlying problem is difficult, it should be difficult in the game for the same underlying reasons.

For example:

> If metallurgy is difficult because metallurgy is genuinely difficult, the game should not replace metallurgy with a simplified crafting recipe.

A player with genuine metallurgical knowledge should have a meaningful advantage over a player who knows nothing.

A player with deep real-world engineering expertise should be capable of achieving things more quickly and reliably than a player who relies exclusively on the game's instructional systems.

This is not considered an unfair balance problem.

**That is the intended fantasy.**

The game should not artificially compensate for the player not knowing real science.

Instead, the knowledge system should make the relevant knowledge available to players who don't already possess it.

The distinction is:

> **knowledge can be taught; physical reality cannot be simplified merely for convenience.**

---

# 5. THE KNOWLEDGE / QUEST SYSTEM

The game will have a comprehensive knowledge/quest system inspired partly by Minecraft modpack quest books.

However, it must not simply be a list of arbitrary recipes.

The system should function as:

* technical reference
* educational system
* progression interface
* dependency graph
* objective planner
* engineering explanation system

It should tell the player:

* what they can attempt
* what capabilities exist
* what prerequisites are required
* why they are required
* what materials are relevant
* what processes are relevant
* what alternative approaches exist
* what capabilities are currently missing
* what can be done next

For example, instead of:

> RESEARCH ELECTRICITY → ELECTRICITY UNLOCKED

the game should conceptually support:

> Goal: Generate useful electrical power.
>
> Possible methods:
>
> * electromagnetic induction
> * chemical generation
> * photovoltaic conversion
> * etc.
>
> Electromagnetic induction requires:
>
> * conductive material
> * magnetic field
> * relative motion
> * appropriate geometry
> * sufficient manufacturing precision
>
> Current missing capabilities:
>
> * refined conductive material
> * suitable magnetic material
> * controlled mechanical rotation
> * adequate manufacturing precision

The knowledge system should therefore represent a **dependency graph of civilization**.

The player is not unlocking arbitrary technologies.
They are constructing the prerequisites that make those technologies possible.

---

# 6. DO NOT MAKE THE GAME A GIANT HAND-AUTHORED TECHNOLOGY TREE

This is one of the most important architectural decisions.

Do not make the game fundamentally depend on hard-coded entries such as:

* Stone Age
* Bronze Age
* Iron Age
* Steam Age
* Electrical Age
* Information Age
* Space Age

Those can exist as descriptive classifications, but the underlying simulation should not depend on them.

Likewise, avoid making:

* generator
* computer
* rocket
* refinery
* semiconductor
* nuclear reactor

fundamentally special "technology unlock" objects.

Instead, implement the **physical and engineering primitives that make such systems possible**.

A rocket should emerge because the simulation allows the player to build a system capable of:

* containing propellant
* generating thrust
* moving mass
* surviving pressure
* surviving temperature
* maintaining structural integrity
* controlling orientation
* etc.

A generator should emerge because:

* mechanical energy
* magnetic field
* conductors
* geometry
* electromagnetic induction

are represented.

A computer should emerge because:

* physical states
* logic
* switching
* information
* memory
* computation
* communication

are represented.

The developer should define **why something works**, rather than manually author every specific future object.

---

# 7. THE FOUR FUNDAMENTAL SIMULATION PRIMITIVES

At the highest level, the simulation should revolve around four broad categories.

## A. STATE

What exists?

Examples:

* mass
* composition
* temperature
* pressure
* volume
* phase
* position
* velocity
* force
* energy
* charge
* electrical potential
* material properties
* structural state
* fatigue
* wear
* corrosion
* information
* etc.

Do not blindly give every object every property.
Use only the state variables necessary to reproduce meaningful consequences.

---

## B. TRANSFORMATIONS

What can change state?

Examples:

* heating
* cooling
* melting
* freezing
* evaporation
* condensation
* combustion
* chemical reaction
* compression
* expansion
* cutting
* grinding
* drilling
* casting
* forging
* extrusion
* separation
* electrical conversion
* mechanical conversion
* energy storage
* transport
* computation
* etc.

Transformations should have:

* inputs
* outputs
* conditions
* energy requirements
* rates
* efficiencies
* constraints
* byproducts
* failure modes
* uncertainty where appropriate

---

## C. CONSTRAINTS

What prevents arbitrary outcomes?

Examples:

* conservation of energy
* conservation of mass
* conservation of momentum
* charge conservation
* thermodynamic constraints
* material strength
* heat limits
* pressure limits
* geometry
* reaction kinetics
* manufacturing tolerances
* machine capacity
* available resources
* etc.

The simulation must be internally consistent.

---

## D. NETWORKS

How are things connected?

Examples:

* electrical circuits
* water systems
* gas systems
* heat networks
* mechanical shafts
* material logistics
* power grids
* communication systems
* manufacturing chains
* railway networks
* industrial systems

A useful generalized abstraction is:

> node + connection + flow + transformation + constraint

This can then be specialized for different domains.

---

# 8. MATERIALS SHOULD BE PROPERTY-DRIVEN

Do not make materials primarily into labels such as:

> Copper = wire material
> Steel = strong metal

Instead, materials should possess meaningful physical properties.

Conceptually:

```text
Material
    composition
    density
    melting behaviour
    specific heat
    thermal conductivity
    electrical conductivity/resistivity
    mechanical strength
    elasticity
    hardness
    toughness
    magnetic behaviour
    chemical behaviour
    radiation behaviour
    phase information
    etc.
```

The exact schema can evolve.

The critical principle is:

> **A material's behaviour should arise from its represented properties, not from arbitrary item-specific rules.**

This allows:

* pure materials
* alloys
* contaminated materials
* different processing histories
* different heat treatments
* different phases
* different defects

to produce meaningfully different outcomes.

Two objects both called "steel" should potentially behave differently if their underlying physical states differ.

---

# 9. PROCESSES MUST PRODUCE DISTRIBUTIONS, NOT PERFECT ITEMS

This is critical.

Real manufacturing does not produce perfectly identical components.

If the player manufactures 10,000 nominally identical bolts, they should not all be perfectly identical internally.

A manufacturing process should instead produce a population with distributions of properties.

For example:

```text
Nominal diameter: 10.000 mm
Mean error: +0.002 mm
Standard deviation: 0.006 mm
Surface finish distribution: ...
Defect population: ...
Strength distribution: ...
```

Another manufacturing line might produce:

```text
Nominal diameter: 10.000 mm
Mean error: +0.0002 mm
Standard deviation: 0.0008 mm
```

Both produce "10 mm bolts," but their populations are different.

The simulation should therefore represent:

> **process capability**

rather than merely:

> item quality.

---

# 10. MANUFACTURING PROCESS CAPABILITY

Manufacturing processes should have measurable characteristics such as:

* dimensional accuracy
* repeatability
* surface finish
* machine rigidity
* tool wear
* thermal stability
* measurement quality
* alignment accuracy
* environmental stability
* automation consistency
* material consistency
* defect generation
* process drift
* calibration
* etc.

Do not make these arbitrary RPG stats unless absolutely necessary.

They should be derived from the player's actual industrial setup.

For example, accuracy can depend on:

* machine quality
* machine rigidity
* tooling
* tool condition
* environmental temperature
* operator variability
* feedback control
* measurement accuracy
* calibration
* manufacturing method

etc.

---

# 11. THE "GOOD BOLT FACTORY" EXAMPLE

The game should NOT contain:

> Accurate Bolt Factory = Rocket Reliability +20%.

Instead:

```text
Raw material
    ↓
Material processing
    ↓
Machining process
    ↓
Dimensional distribution
Surface-finish distribution
Defect distribution
    ↓
Inspection
    ↓
Accepted component population
    ↓
Assembly process
    ↓
Bolt preload / joint properties
    ↓
Structural system reliability
    ↓
Rocket behaviour
```

This is the general pattern.

A manufacturing process changes the statistical properties of the components it produces.

Those components change the behaviour of systems that contain them.

The system's reliability emerges from that chain.

---

# 12. MANUFACTURING HISTORY MATTERS

Components should be able to inherit meaningful history.

Conceptually:

```text
material batch
manufacturer
machine line
machine condition
tool condition
heat-treatment batch
process parameters
inspection status
installation process
maintenance history
operating environment
etc.
```

This allows correlated failures.

For example:

A furnace calibration problem creates a bad heat-treatment batch.
That batch produces poor bolts.
Those bolts enter a rocket assembly.
The rocket later experiences a failure.

The game should ideally be able to explain:

> structural failure
> → fastener fatigue
> → manufacturing batch X
> → heat-treatment deviation
> → furnace temperature-control instability
> → calibration problem

This is vastly more desirable than:

> random rocket failure.

---

# 13. QUALITY CONTROL AND METROLOGY MUST MATTER

The player's civilization should be able to reduce uncertainty by improving:

* measurement
* inspection
* calibration
* quality control
* process monitoring
* environmental control
* automation
* statistical process control
* non-destructive testing

This should not be represented simply as:

> Quality Control Level 5.

Instead, the physical process should improve.

For example:

Without inspection:

> defective components may enter the assembly process.

With dimensional gauges:

> certain classes of defects are rejected.

With precision metrology:

> subtler defects are identified.

With ultrasonic inspection:

> internal defects can be detected.

With automated closed-loop manufacturing:

> process drift is detected and corrected.

This is part of technological advancement.

---

# 14. PHYSICAL QUALITY AND KNOWLEDGE OF QUALITY ARE DIFFERENT

The game should distinguish:

### Actual state

What is physically true.

### Player/civilization knowledge

What the civilization knows about that state.

A factory might actually have:

> 0.02% defect rate

while the player believes:

> 0.001%.

The civilization therefore has poor knowledge despite good physical capability.

Conversely, a mediocre process with excellent inspection can produce high-quality outgoing components because defects are identified and rejected.

This means:

> **metrology and measurement are technologies in their own right.**

---

# 15. RELIABILITY IS A SYSTEM PROPERTY, NOT A MAGIC STAT

Reliability should propagate through the hierarchy:

```text
Component
    ↓
Joint
    ↓
Subsystem
    ↓
Stage
    ↓
Vehicle
    ↓
Mission
```

However:

**Do not simply multiply arbitrary probabilities.**

Failure can be correlated.

For example:

* common manufacturing batch
* common calibration error
* common environmental condition
* shared power supply
* shared control software
* shared structural load
* common maintenance failure

Therefore the reliability model should preserve causal/common-cause relationships.

Redundancy should help only when the redundant systems are actually sufficiently independent.

---

# 16. BUILD SYSTEMS AS FIRST-CLASS ENTITIES

A rocket should not merely be 4,800 unrelated objects.

It should be a hierarchical system.

Conceptually:

```text
Civilization
    ↓
Launch infrastructure
    ↓
Launch vehicle
    ↓
Rocket
    ↓
Stage
    ↓
Engine
    ↓
Turbopump
    ↓
Bearing
```

A system should have:

* components
* interfaces
* inputs
* outputs
* state
* constraints
* failure modes
* reliability
* maintenance requirements
* observable behaviour
* internal hierarchy

Systems should be able to expose aggregate behaviour upward.

This enables enormous complexity without requiring the simulation to inspect every microscopic component every tick.

---

# 17. HIERARCHICAL CAUSALITY

This is a central concept.

The simulation should know:

> what things can affect what other things, at what resolution, and under what circumstances.

A rocket can expose:

```text
Structure: nominal
Propulsion: nominal
Electrical: nominal
Guidance: nominal
Thermal: nominal
```

Propulsion can expose:

```text
Engine 1: nominal
Engine 2: nominal
Fuel feed: nominal
Oxidizer feed: nominal
```

An engine can expose:

```text
Chamber: nominal
Turbopump: degraded
Bearings: nominal
Valves: nominal
```

Only when something becomes causally relevant should the simulation descend to more detailed state.

This gives the game an:

> **explanation tree**

and:

> **causal hierarchy**

---

# 18. ADAPTIVE SIMULATION / SIMULATION LOD

The game cannot and should not simulate every atom.

The principle is:

> **simulate causes at the lowest resolution necessary to predict consequences at the player's resolution.**

This is the fundamental solution to the infinite-detail problem.

Graphics already use level-of-detail.
Simulation should have an analogous system.

For example:

A factory 5,000 km away may be represented by:

```text
Steel production: 42,000 tonnes/day
Energy consumption: 840 MW
Inputs: iron ore + carbon + flux
Outputs: steel + slag + heat
```

When the player is physically present, the factory can be represented by detailed machines.

Likewise, a rocket can normally use aggregate subsystem models.

If vibration becomes abnormal in a propulsion system, the simulation can "promote" relevant components to higher fidelity.

Do not attempt to permanently run maximum simulation detail everywhere.

---

# 19. CAUSAL CULLING

If a hidden microscopic state cannot plausibly affect anything observable under the current conditions, it should not consume high simulation fidelity.

For example:

A perfectly healthy bolt in a non-critical low-load panel does not need to be individually simulated at maximum detail during every frame.

A bearing experiencing unusual vibration in a high-performance rocket engine does.

This principle should govern simulation cost.

---

# 20. THREE KINDS OF EVENTS

Distinguish:

## Deterministic events

The outcome is effectively guaranteed.

Example:

> A structural element is loaded vastly beyond its strength.

Do not roll dice.

## Stochastic physical events

Reality itself contains uncertainty / variation.

Example:

> microscopic defect propagation / component lifetime variation.

Use probability distributions.

## Epistemic uncertainty

The player/civilization does not know some information.

Example:

> a component contains a hidden manufacturing defect.

The simulation knows the underlying state; the player may not.

Do not confuse lack of player knowledge with random physical behaviour.

---

# 21. RNG SHOULD REPRESENT REAL UNCERTAINTY, NOT GAME DESIGN

Avoid:

> "There is a 5% chance the rocket explodes because rockets are dangerous."

Instead:

> component/material/process/environment state creates a probability distribution over possible outcomes.

Better engineering should change those distributions.

Examples:

* better material control → fewer defects
* better machining → tighter dimensions
* better inspection → more defects removed
* better installation → better preload
* better environmental control → less drift
* better redundancy → lower catastrophic probability
* better maintenance → less degradation
* better shielding → lower radiation risk

The RNG is therefore the final unresolved component of a causal model.

---

# 22. FAILURE PROPAGATION

Systems should allow failures to propagate through real dependencies.

Conceptually:

```text
Bolt failure
↓
Joint loosens
↓
Pipe vibrates
↓
Seal degrades
↓
Fuel leak
↓
Mixture ratio changes
↓
Combustion instability
↓
Engine thrust loss
↓
Trajectory deviation
↓
Mission failure
```

Do not hard-code:

> "If bolt #439 fails, explode rocket."

The desired outcome is that the failure emerges from the system's relationships.

---

# 23. "WHY DID IT FAIL?" MUST BE A FIRST-CLASS CAPABILITY

The player should ideally be able to inspect failures.

Not necessarily immediately.

The level of explanation should depend on available measurement and diagnostic capability.

For example:

> Rocket failure
> → Propulsion failure
> → Engine 2
> → Oxidizer turbopump
> → Bearing degradation
> → Excessive vibration
> → Manufacturing defect
> → Heat-treatment deviation
> → Furnace calibration failure

This is both:

* computationally useful
* educational
* satisfying
* essential for debugging a civilization's industrial processes

The player should learn from failures rather than simply receiving unexplained bad RNG.

---

# 24. THE GAME'S REALISM TARGET

Do not attempt to achieve literal reality.

That is impossible.

Instead define the target as:

> **causal fidelity**

The simulation should preserve the relationships that matter at the level the player can meaningfully interact with.

A sufficiently good simulation is one where:

1. causes produce appropriate consequences;
2. consequences propagate through appropriate systems;
3. relevant uncertainty is represented;
4. irrelevant microscopic detail is abstracted;
5. player decisions produce believable differences;
6. the simulation remains internally consistent;
7. real-world expertise transfers into the simulation where intended.

The game should never need to know the exact position of every electron in a copper wire.

It needs to know enough to correctly reproduce the wire's relevant:

* voltage
* current
* resistance
* heat
* power
* failure behaviour

etc.

---

# 25. STATE COMPRESSION

Think of the game's simulation as **lossy compression with causality preserved**.

Reality:

> approximately 10²⁷ atoms in a macroscopic object.

Game representation:

```text
material
mass
temperature
stress
strain
fatigue
defect state
etc.
```

If those variables preserve the relevant future consequences, the abstraction is valid.

Do not pursue microscopic realism unless microscopic behaviour can affect a meaningful player-visible outcome.

---

# 26. OBSERVABLE / OPERATIONAL / LATENT STATE

Consider giving simulation entities three conceptual categories of state.

## Observable state

What the player can directly observe.

Example:

> bolt appears intact.

## Operational state

What the simulation needs to predict behaviour.

Example:

> preload, stress, temperature, fatigue.

## Latent state

Hidden state that may eventually become relevant.

Example:

> microscopic defect population.

This allows the simulation to preserve hidden uncertainty without fully resolving microscopic physics.

---

# 27. TECHNOLOGY ALSO INCREASES THE ABILITY TO MEASURE REALITY

The player should progressively gain better ability to observe what is actually happening.

Early:

> visual inspection.

Later:

> thermometers, balances, gauges.

Later:

> precision metrology.

Later:

> electronic sensors.

Later:

> oscilloscopes.

Later:

> advanced non-destructive testing.

Later:

> increasingly sophisticated diagnostics.

The civilization should not only become more powerful.
It should become **more capable of knowing what is actually happening**.

This also ties into reliability.

---

# 28. TECHNOLOGICAL PROGRESSION CAN BE UNDERSTOOD AS:

> **power × control × knowledge × reliability**

not merely:

> "more energy."

This fits naturally with the Kardashev-scale progression.

Early:

> fire

Then:

> controlled furnace

Then:

> precise thermal control

Then:

> industrial process control

Then:

> highly automated manufacturing

Then:

> planetary-scale industrial control

etc.

---

# 29. PROCESS CAPABILITY SHOULD BE A GENERAL ENGINE CONCEPT

A generalized manufacturing/process model can conceptually include:

```text
inputs
outputs
nominal targets
variation
bias
limits
failure modes
rate
energy consumption
tool wear
environmental sensitivity
measurement uncertainty
```

Examples:

### Machining

* nominal dimensions
* dimensional error distribution
* surface roughness
* tool wear
* thermal stability

### Heat treatment

* target temperature
* temperature variance
* heating rate
* cooling rate
* atmosphere
* uniformity

### Assembly

* positioning error
* fastening variance
* contamination probability
* inspection coverage

These processes should produce components whose properties reflect the process.

---

# 30. CLOSURE AND REPRODUCIBILITY

A technology should not become truly "available" merely because the player successfully makes one prototype.

The game should distinguish:

### Discovery

Can it be done once?

### Reproducibility

Can it be done again?

### Scalability

Can it be done in quantity?

### Automation

Can machines do it?

### Independence

Can the civilization sustain the entire supply chain and maintenance?

This is an important distinction.

The first working computer does not mean the civilization has a functioning computer industry.

The first working rocket does not mean the civilization has reliable launch capability.

A technology becomes truly transformative when it becomes **repeatable and maintainable**.

---

# 31. SCALE IS A PRIMARY PROGRESSION AXIS

The civilization should progress roughly through:

> one human
> → human + tools
> → human + machines
> → powered machines
> → automated machinery
> → factories
> → automated industrial systems
> → robotic industry
> → planetary infrastructure
> → extraterrestrial industry
> → stellar industry

The player gradually substitutes machine productivity for personal labour.

This is especially important because the player begins alone.

---

# 32. THE GAME SHOULD SCALE FROM OBJECTS TO SYSTEMS

Early:

* one tool
* one furnace
* one shelter
* one mine

Mid:

* factories
* power plants
* railways
* automated mines
* logistics systems

Late:

* planetary power networks
* interplanetary logistics
* orbital manufacturing
* lunar industry
* asteroid processing
* large-scale energy systems

As scale increases, individual objects should increasingly become aggregate quantities / nodes.

---

# 33. KARDASHEV SHOULD BE A MEASUREMENT, NOT A LEVEL

Kardashev should be an emergent civilizational metric.

Do not make:

> KARDASHEV 1 = technology unlock.

Instead measure actual energy utilization.

Conceptually:

```text
civilization energy utilization = X W
```

and derive a civilization-scale classification.

Informal Type 0 may be used for sub-Type-I civilization, with awareness that the original Kardashev scale formally defined Types I–III.

The player's civilization should potentially progress:

> Type 0 → Type I → Type II → Type III → beyond

The number should describe what has happened, not cause it.

---

# 34. THE GAME MUST NOT REQUIRE AI AT RUNTIME

The development process may use AI tools extensively.

AI can help:

* research real-world science
* identify relevant variables
* collect domain knowledge
* discover failure modes
* generate documentation
* write code
* write tests
* suggest implementations
* compare reference information
* help iterate architecture

However, the game itself should NOT depend on an LLM to decide what physics means or what happens in the world.

The runtime model should preferably be:

* explicit
* deterministic where appropriate
* statistical where appropriate
* inspectable
* testable
* debuggable
* reproducible

AI helps develop the model.
The model itself governs the game.

---

# 35. DOMAIN EXPERTISE SHOULD BECOME DEVELOPMENT INPUT

For each scientific/engineering domain, create a formal domain specification.

For example, metallurgy should eventually document:

### Inputs

* ore composition
* impurities
* atmosphere
* temperature
* pressure
* processing history

### Processes

* extraction
* melting
* alloying
* casting
* forging
* heat treatment
* cooling

### State variables

* composition
* phase fractions
* hardness
* toughness
* residual stress
* defects
* grain-related properties where appropriate

### Failure mechanisms

* fatigue
* fracture
* corrosion
* creep
* thermal failure
* etc.

### Measurements

* dimensional inspection
* hardness testing
* microscopy
* ultrasonic testing
* etc.

This domain specification becomes the blueprint the code implements.

The same principle should eventually apply to:

* hydrology
* geology
* chemistry
* metallurgy
* thermodynamics
* mechanical engineering
* electrical engineering
* electronics
* computing
* civil engineering
* aerospace
* orbital mechanics
* biology
* ecology
* etc.

Do not implement all domains at once.
Build them progressively.

---

# 36. PHYSICS VALIDATION

Every domain should have reference tests.

Examples:

### Mechanics

Compare against analytical mechanics cases.

### Electricity

Compare against known circuit equations.

### Heat transfer

Compare against benchmark solutions.

### Orbital mechanics

Compare against analytical/reference trajectories.

### Materials

Compare against published physical data.

### Fluid behaviour

Compare against validated simplified cases.

### Rocket performance

Compare against known engineering calculations and reference systems.

The objective is not:

> "Is it realistic?"

The objective is:

> **Does the model reproduce known relationships within an explicitly defined error budget?**

---

# 37. ERROR BUDGETS

Every subsystem should explicitly define:

### What it models accurately enough

and:

### What it intentionally abstracts.

For example:

| Domain                           | Appropriate target         |
| -------------------------------- | -------------------------- |
| orbital mechanics                | very high fidelity         |
| structural failure               | high fidelity              |
| electrical power systems         | high fidelity              |
| materials behaviour              | domain-dependent           |
| atmospheric behaviour            | statistical/approximate    |
| individual electron trajectories | intentionally not modelled |
| individual plant cells           | intentionally not modelled |
| microscopic crystal physics      | only where necessary       |

Do not try to model everything equally.

---

# 38. CAUSAL FIDELITY > RAW DETAIL

A massively detailed system can still be unrealistic if the wrong variables are being simulated.

A much simpler model can be highly convincing if it preserves the right causal relationships.

This is the central target:

> **not microscopic fidelity, but causal fidelity.**

---

# 39. WHAT THE GAME SHOULD FEEL LIKE

The desired player experience is:

> "I know this subject in real life, and the game respects that knowledge."

An expert should be able to exploit genuine understanding of:

* terrain
* water
* materials
* machines
* chemistry
* electronics
* mathematics
* programming
* aerospace
* etc.

The game should reward **knowledge of reality**, not knowledge of game-specific recipes.

A person with no expertise can use the knowledge system to learn.

A person with real expertise should be able to bypass much of that learning because they already know it.

This difference is intentional.

---

# 40. UI / HUD DIRECTION

The game's interface should begin with a familiar first-person survival interface, but become increasingly sophisticated as the civilization grows.

Early:

* crosshair
* hands
* hotbar
* personal inventory
* health / physical condition
* temperature
* hunger/thirst or equivalent survival systems
* contextual interaction

The crosshair should remain a powerful interaction mechanism.

Looking at a tree, rock, machine, wire, furnace, structure or vehicle should allow the player to inspect/interact with it.

---

# 41. INVENTORY SHOULD BE SCALE-DEPENDENT

Early:

> personal inventory makes sense.

Late:

> civilization cannot realistically fit into a character inventory.

The representation should transition:

> carried objects → containers → stockpiles → warehouses → logistics systems → material flows

The late game should not remain "Minecraft but with a bigger backpack."

---

# 42. ENGINEERING UI

The player should eventually have tools/interfaces for inspecting:

* energy
* power
* voltage
* current
* temperature
* pressure
* flow
* material state
* machine condition
* wear
* maintenance
* faults
* control signals
* production throughput

The UI should expose increasingly detailed information as the civilization gains corresponding measurement capabilities.

---

# 43. CIVILIZATION UI

At larger scales, provide interfaces for:

* resource extraction
* production
* logistics
* energy networks
* industry
* automation
* infrastructure
* material throughput
* energy utilization
* geographical expansion
* orbital infrastructure
* extraterrestrial infrastructure
* Kardashev-scale metrics

The player should be able to move conceptually among:

> person → machine → facility → region → planet → orbital system → interplanetary system

without changing the underlying civilization.

---

# 44. THE CORE ARCHITECTURE SHOULD LOOK CONCEPTUALLY LIKE THIS

```text
WORLD
│
├── geography
├── climate
├── geology
├── hydrology
├── biology/ecology
├── astronomy
│
▼
PHYSICAL STATE
│
├── matter
├── energy
├── temperature
├── pressure
├── motion
├── composition
├── information
│
▼
PRIMITIVES
│
├── transformations
├── networks
├── constraints
├── measurements
│
▼
COMPONENTS
│
├── materials
├── tools
├── machines
├── processes
│
▼
SYSTEMS
│
├── assemblies
├── factories
├── vehicles
├── infrastructure
│
▼
CAPABILITIES
│
├── what the civilization can actually do
├── what it can reproduce
├── what it can measure
├── what it can reliably maintain
│
▼
GOAL PLANNER
│
├── player goal
├── dependency resolution
├── alternative pathways
├── missing capability identification
│
▼
KNOWLEDGE / QUEST UI
│
├── explanations
├── objectives
├── dependency graphs
├── diagnostics
│
▼
PLAYER
```

The renderer and conventional game UI sit above the simulation.

---

# 45. MOST IMPORTANT PROTOTYPE PRIORITY

Do NOT begin by attempting to build the entire game.

Do NOT begin with:

* the whole Earth
* all flora/fauna
* space
* Kardashev progression
* the entire quest book
* thousands of technologies
* beautiful final UI

First prove the **simulation architecture**.

The most important initial vertical slice should be something like:

> **A player can manufacture components using a physical process; the quality of that process changes component distributions; those components are assembled into a machine/system; the system's reliability and behaviour change accordingly; failures can propagate causally; the game can explain why something failed.**

The bolt/rocket example is ideal.

For instance:

1. Create a primitive machining process.
2. Produce batches of bolts with distributions of dimensional properties.
3. Allow the player to improve machining precision.
4. Allow measurement/inspection.
5. Allow assembly into joints.
6. Give the joints realistic-ish load/preload/fatigue behaviour.
7. Build a simple engine/vehicle/system containing those joints.
8. Stress it.
9. Observe failures.
10. Improve the manufacturing system.
11. Observe that failure behaviour changes.

If that works, you have proven one of the most important principles in the entire game.

---

# 46. THE SINGLE MOST IMPORTANT DESIGN TEST

For every feature, ask:

> **Can the player make a meaningful decision differently because of this information or physical behaviour?**

If not, abstract it.

If yes, simulate it.

Examples:

> Exact electron positions in copper wire?

Not relevant → abstract.

> Resistance causing heat?

Relevant → simulate.

> Microscopic defect changing bearing lifetime?

Potentially relevant → represent statistically.

> Exact state of an irrelevant bolt 2,000 km away?

Not relevant → aggregate.

> State of a critical engine bearing under abnormal vibration?

Relevant → increase simulation detail.

This should be the filter preventing infinite scope.

---

# 47. DO NOT LET ABSTRACT MODELS CREATE EXPLOITABLE CONTRADICTIONS

A simplified model is acceptable.

A contradictory model is not.

Players will forgive:

> "This is an approximation."

They will not forgive:

> "I found a way to create infinite energy because the simplified system forgot to conserve it."

Therefore all abstraction layers must preserve meaningful invariants and constraints.

The game should be particularly strict about:

* conservation of energy
* conservation of mass
* conservation of momentum
* dimensional consistency
* thermodynamic constraints
* structural constraints
* resource accounting
* causal relationships

---

# 48. THE TARGET REALISM HIERARCHY

Think about realism in five levels:

### Level 1 — Cosmetic realism

It looks right.

### Level 2 — Behavioural realism

It behaves plausibly.

### Level 3 — Causal realism

It behaves plausibly for the correct reasons.

### Level 4 — Transferable realism

Real-world knowledge transfers into the game.

### Level 5 — Predictive realism

A knowledgeable player can use the simulation to make quantitatively useful predictions.

The project should strongly target:

> **Level 4**

and reach:

> **Level 5**

where practical and worthwhile.

Level 5 is not required universally.

Do not waste enormous resources chasing predictive microscopic accuracy where it has no gameplay value.

---

# 49. THE GAME'S CENTRAL PHILOSOPHY

Keep these principles in mind whenever architecture or implementation decisions are being made:

> **Do not simulate technologies. Simulate the reasons technologies work.**

> **Do not make knowledge a substitute for physical capability.**

> **Do not make physical capability a magic unlock.**

> **Do not simulate microscopic reality when its consequences can be represented with a valid higher-level model.**

> **Do not add randomness merely to create difficulty. Use stochasticity to represent genuine uncertainty or unresolved microscopic variation.**

> **Do not let expertise become irrelevant because the game simplifies away the subject.**

> **Do not make reality difficult artificially. Represent the causes of difficulty and allow difficulty to emerge.**

> **Do not make the player "unlock" reliability. Make reliability emerge from manufacturing, measurement, maintenance, redundancy, materials and process control.**

> **Do not try to simulate everything equally. Allocate simulation fidelity according to causal relevance.**

> **Do not require the developer to author every future technology. Build a system expressive enough that future technologies can emerge from the primitives.**

---

# 50. THE ULTIMATE THESIS

The game is fundamentally asking:

> **How far can one human being take civilization when starting from nothing on an untouched present-day Earth, if the laws of physics remain real and the player is forced to construct every capability required to progress?**

The player's civilization advances not because the game says:

> "Technology unlocked."

It advances because the player has succeeded in creating:

* better materials
* better tools
* better processes
* better machines
* better measurements
* better manufacturing
* better automation
* better reliability
* better energy systems
* better logistics
* greater productive capacity

The result is a civilization whose increasing capabilities are consequences of the underlying model.

The progression is therefore:

> **knowledge → capability → control → reliability → scale**

and eventually:

> **scale → civilization → planetary energy → space industry → Kardashev progression**

The ultimate goal of the engine is not to contain every technology humanity has ever invented.

It is to contain enough of the **physical language of reality** that technologies the developer never explicitly authored can nevertheless become possible.

The ideal end-state is:

> **The developer does not tell the game what the player can invent. The developer defines the rules under which invention is possible.**

Build toward that principle incrementally, test it scientifically, and do not sacrifice causal consistency merely to make implementation easier.
