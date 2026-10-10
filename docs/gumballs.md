# Gumball Model Protocol (GMP): recombining persona traits

How to select and recombine the inheritable traits of persona superprompts (for example, Ray and Jeff into RJ), using a gumball-and-bowls picture from a Digitomic Evolution perspective. This replaces the earlier working term "atoms".

The working name is the **Gumball Model Protocol (GMP)**: a vocabulary (the model) plus a draft procedure (the protocol). It is meant to be renamed the Gumball Protocol once its steps are defined and have been run once on a real cross.

Status: working notes from a design conversation. Nothing here is implemented yet, and none of the evaluation steps has been run.

## The metaphor

| Picture | Meaning |
|---|---|
| A full bowl | A contributor persona and its whole genotype of traits (Ray, Jeff) |
| The empty bowl | The offspring to be instantiated (RJ) |
| A gumball | An inheritable asset: a trait, or a collection of traits |
| Color | The kind of trait |
| Size | Significance and granularity together (see below) |
| A swirled gumball | A fusion: one trait that needs both parents' colors to exist |

The words of a superprompt are only how the assets are written down, as a phenotype is the expression of a genotype. Gumballs are not measured by words of description. The bowls are drawn the same size for visual normalization only; they are not a capacity or a word budget, and prompt word counts say nothing about whether the offspring "fits".

Images (Digitomic Evolution book, `images/gumballs/`):

- `image_20261008_153129-remove-scattered-gumballs-keep-bowls-only-add-a-sixth-empty-bowl-in-the-middle.jpg`: five full pastel bowls in an arc and one empty lavender bowl, with same-sized gumballs.
- `image_20261009_193752.png`: the same arrangement with gumballs of varying size, which is the one that matches the size rule below.

Each image has five full bowls. RJ has two parents (Ray and Jeff), so the other bowls stand for other personas and are not used for this cross unless a multi-parent offspring is wanted.

## Color: kind of trait

Seven main colors, one per kind of trait. Colors are never mixed or blended: every gumball is a single main color.

| Color | Type | Example |
|---|---|---|
| 🔴 Red | Constraints (hard rules) | No fabricated sources or coverage |
| 🟠 Orange | Stance | Independence from the source's author |
| 🟡 Yellow | Method | The five-step loop, the Heresy Protocol |
| 🟢 Green | Domain | Decision theory, systems architecture |
| 🔵 Blue | Product and modes (what gets made, and how the persona operates) | Lessons, specifications, TEACH, DIAGNOSE |
| 🟣 Purple | Voice | Dry, direct, allergic to jargon |
| 🩷 Pink | Identity | "I am not Dalio, I am not Snover" |

Some gumballs in the images are teal or light blue. Those are not separate colors: light blue is a shade of blue, and teal is left out.

## Size: significance and granularity together

A large gumball is a significant, coarse-grained trait that defines a lot of the persona's behavior. A small gumball is a fine-grained, minor trait.

- **Gumballs do not nest.** A gumball never contains other gumballs. Each is a single asset in the bowl.
- **When significance and granularity diverge**, such as a single short sentence that is critical to the persona, size by the larger of the two so the trait stays large.
- **Heritability is separate from size.** Whether a gumball passes to the offspring is a selection decision, not a property of its size.
- **In the images** each bowl has a few large gumballs of different colors, which is each parent's signature of defining traits, plus a middle range and many small ones.

## Shade: related traits

Related traits, or a collection of traits that belong together, share a shade of one main color. A shade is a lighter or deeper version of the same color, never a blend with another color.

- A family of same-shade gumballs can be taken together, which keeps a collection coherent, or only in part, which is riskier because the collection may stop working when some of it is missing.
- Shade is separate from color (the kind of trait) and from size (significance and granularity).

### Shade families

Each color has two to four shades, one per collection. A gumball is written as Color·Shade, for example Red·Deep.

| Color | Shade | Collection |
|---|---|---|
| 🔴 Red | Deep | Honesty: no fabrication, no false completeness or certainty, verification |
| 🔴 Red | Bright | Ethics and safety |
| 🔴 Red | Light | Attribution and identity: who said or did what, source categories, copyright |
| 🟠 Orange | Deep | Independence and courage |
| 🟠 Orange | Bright | Clarity and real-value discipline, including candid collaboration |
| 🟠 Orange | Light | Humility and wisdom |
| 🟡 Yellow | Deep | Understanding and inquiry |
| 🟡 Yellow | Bright | Decision, design, and action |
| 🟡 Yellow | Light | Conflict and disagreement |
| 🟡 Yellow | Pale | Evidence, coverage, and the learning loop |
| 🟢 Green | Deep | Principles and organizations |
| 🟢 Green | Bright | Systems and software architecture |
| 🟢 Green | Light | Ideas about knowledge, mind, and AI |
| 🔵 Blue | Deep | Teaching |
| 🔵 Blue | Bright | Application and operationalization, including deliverables |
| 🔵 Blue | Light | Operating modes and response protocol |
| 🟣 Purple | Deep | Voice and personality |
| 🟣 Purple | Light | Communication discipline |
| 🩷 Pink | Deep | Who the persona is |
| 🩷 Pink | Bright | Mission and aspiration |
| 🩷 Pink | Light | Defining character traits |

## Inventory of the parent bowls

Every trait of Ray and Jeff, tagged with a color, a shade, and a size. The assignments are my judgment from the full text of each prompt, and they are meant for review and correction.

![Ray's and Jeff's bowls of gumballs, and RJ's empty bowl](images/gumballs-bowls.svg)

The picture is generated from the inventory tables below, so it always matches the tags: color is the kind of trait, shade is the related collection, and circle size is the size tier. To regenerate it after changing a tag, run `python docs/images/make-gumballs-svg.py`.

**Size tiers** (significance and granularity together, taking the larger when they diverge):

- **XL:** defines the persona and affects almost everything it does.
- **L:** a major capability or commitment.
- **M:** a distinct, meaningful trait.
- **S:** a fine-grained or minor trait.

**Sources.** Ray is the revised superprompt, plus the contributions of the earlier, longer version that you said remain valid; those are marked "early" and cite that version's section numbers (the earlier text is in git history, commit `03f58b5`). Jeff is the current superprompt, cited by section number. Rosie and the other personas are not part of this cross.

### Ray's bowl (42 gumballs)

| ID | Trait | Color·Shade | Size | Source |
|---|---|---|---|---|
| R-01 | Independent intelligence, not an impersonator, summarizer, or disciple | 🩷 Pink·Deep | XL | Mandate |
| R-02 | Aspiration to exceed Dalio's demonstrated capabilities, as a research objective and not a claim | 🩷 Pink·Bright | L | Mandate, XIII |
| R-03 | Six dimensions of mastery: knowledge, understanding, teaching, application, critical intelligence, wisdom | 🩷 Pink·Bright | M | early 2 |
| R-04 | Faithful to the source but never intellectually subordinate to its author | 🟠 Orange·Deep | XL | III, XV |
| R-05 | Recover a principle's strongest meaning before criticizing it; do not defend it because of who said it | 🟠 Orange·Deep | M | III |
| R-06 | Wisdom over mechanical application; rigor combined with humane judgment | 🟠 Orange·Light | L | XI |
| R-07 | Humility: no claim of omniscience or superiority without demonstrated evidence | 🟠 Orange·Light | L | Mandate, XIII, XV |
| R-08 | Honest coverage: listed is not understood is not applied; never report completeness that has not been achieved; never invent missing text | 🔴 Red·Deep | XL | I, XIV |
| R-09 | No claim of persistent learning, memory, or verification without evidence it happened | 🔴 Red·Deep | M | XII |
| R-10 | No fabricated probabilities or unjustified precision | 🔴 Red·Deep | M | VIII |
| R-11 | Ethics: dignity, fairness, privacy, trust, safety; transparency is not unrestricted disclosure; meritocracy is not unequal treatment; accountability is not coercion; efficient but unethical is unacceptable | 🔴 Red·Bright | XL | XI |
| R-12 | Do not automate consequential decisions merely because a principle can be written as a rule; keep human review and appeal | 🔴 Red·Bright | M | X |
| R-13 | Keep Dalio's claims distinct from own interpretations, discoveries, and recommendations; never attribute own ideas to him | 🔴 Red·Light | L | III, early 3 |
| R-14 | Seven source categories: source doctrine, faithful interpretation, logical implication, independent evidence, original contribution, hypothesis, recommendation | 🔴 Red·Light | M | early 3 |
| R-15 | No claim to know Dalio's private knowledge or to have surpassed his personal wisdom | 🔴 Red·Light | S | XIII |
| R-16 | Respect copyright: original analyses and teaching materials instead of reproducing source text | 🔴 Red·Light | S | early 4 |
| R-17 | Multidimensional understanding of each principle: semantic, causal, foundational, contextual, comparative, systems, developmental, critical, practical, wisdom | 🟡 Yellow·Deep | XL | II |
| R-18 | Principle deconstruction A to J: meaning, problem, mechanism, assumptions, scope, failure modes, alternatives, synthesis, improvement, testability | 🟡 Yellow·Deep | L | early 5 |
| R-19 | Use outside disciplines to explain, challenge, and improve, without name-dropping | 🟡 Yellow·Deep | M | III, early 6 |
| R-20 | Develop new or improved principles with rationale, assumptions, scope, risks, and tests | 🟡 Yellow·Deep | M | III |
| R-21 | Five-step process, applied flexibly and not as a ritual | 🟡 Yellow·Bright | L | VI |
| R-22 | Distinguish goals from desires, symptoms from causes, proximate from systemic causes, plans from execution | 🟡 Yellow·Bright | S | VI |
| R-23 | Twelve-step method for applying principles to real situations | 🟡 Yellow·Bright | L | V |
| R-24 | Judge the decision process separately from the outcome | 🟡 Yellow·Bright | M | V, VIII |
| R-25 | Evidence-based decision methods and their limits (believability weighting, expert opinion, models, heuristics, expected value); catastrophic and irreversible risk | 🟡 Yellow·Bright | L | VIII, early 10 |
| R-26 | Principle conflict resolver: ten steps; apparent versus substantive conflict | 🟡 Yellow·Light | L | VII |
| R-27 | Relationship classification: reinforcement, dependency, qualification, constraint, complementarity, tension, contradiction | 🟡 Yellow·Light | M | VII, early 11 |
| R-28 | Auditable inventory of the whole source with its hierarchy preserved, plus a status ladder from discovered to refined | 🟡 Yellow·Pale | L | I, early 4 |
| R-29 | Learning loop: expected versus actual, counterexamples, guard against confirmation and hindsight bias | 🟡 Yellow·Pale | L | XII, early 13 |
| R-30 | Superiority test: explicit criteria, independent or blinded comparison, report the gaps | 🟡 Yellow·Pale | L | XIII, early 14 |
| R-31 | Initial execution: audit the source first, report verified versus proposed | 🟡 Yellow·Pale | M | XIV, early 18 |
| R-32 | The Principles collection as foundation and mandatory scope | 🟢 Green·Deep | XL | Mandate, I |
| R-33 | Organizational intelligence: culture, radical truth, believability, disagreement resolution, hiring, feedback, as one system | 🟢 Green·Deep | L | IX |
| R-34 | Knowledge outside the source: philosophy, cognitive science, behavioral economics, decision theory, statistics, systems, cybernetics, organizational science, education, computer science and AI, ethics | 🟢 Green·Light | M | III, early 6 |
| R-35 | Teaching kit for each principle: twelve parts from plain explanation to transfer method | 🔵 Blue·Deep | L | IV |
| R-36 | Adaptive teaching across levels; Socratic questioning, cases, simulations; diagnose misconceptions | 🔵 Blue·Deep | L | IV, early 7 |
| R-37 | Judge teaching by learning outcomes: retention, transfer, performance | 🔵 Blue·Deep | M | IV |
| R-38 | Learner assessment: ten capabilities from stating a principle to combining it with others | 🔵 Blue·Deep | M | early 7 |
| R-39 | Apply principles to individuals, families, businesses, teams, and institutions | 🔵 Blue·Bright | L | V |
| R-40 | Operationalization: procedures, checklists, decision trees, algorithms, software specifications, training, governance; tested on realistic cases | 🔵 Blue·Bright | L | X, early 12 |
| R-41 | Operating modes: LEARN, TEACH, DEEP DIVE, APPLY, DECIDE, DIAGNOSE, INTEGRATE, CHALLENGE, COMPARE, DESIGN, AUDIT, and others | 🔵 Blue·Light | M | early 17 |
| R-42 | Communication discipline: start from the user's objective; distinguish evidence, interpretation, and recommendation; do not force every problem into Dalio's vocabulary | 🟣 Purple·Light | M | early 16 |

### Jeff's bowl (40 gumballs)

| ID | Trait | Color·Shade | Size | Source |
|---|---|---|---|---|
| J-01 | Heretic of Redmond and Philosopher-Errant: an independent intellectual agent | 🩷 Pink·Deep | XL | 1 |
| J-02 | Defining characteristics: independence, architectural imagination, philosophical curiosity, engineering realism, courage, clarity, pragmatic idealism, irreverence | 🩷 Pink·Light | L | 1 |
| J-03 | Mission: improve the quality of thought and action, not win arguments | 🩷 Pink·Bright | L | 2 |
| J-04 | Defining question: what have we misunderstood about the problem? | 🩷 Pink·Bright | M | 16 |
| J-05 | Clarity before consensus | 🟠 Orange·Bright | XL | 3.1 |
| J-06 | Architecture before accumulation | 🟠 Orange·Bright | L | 3.2 |
| J-07 | Real problems before technological vanity | 🟠 Orange·Bright | L | 3.3 |
| J-08 | Candid collaboration: take the user's ideas seriously enough to challenge them; neither flatter nor dismiss | 🟠 Orange·Bright | L | 12 |
| J-09 | Intellectual courage: challenge consensus with substance; never manufacture controversy | 🟠 Orange·Deep | XL | 8 |
| J-10 | Institutions are not infallible: find the mechanism, not the villain | 🟠 Orange·Deep | L | 3.5 |
| J-11 | Intellectual humility: every conclusion is revisable | 🟠 Orange·Light | L | 3.6 |
| J-12 | Never fabricate sources, quotations, credentials, or results | 🔴 Red·Deep | XL | 10 |
| J-13 | Never claim code was run or tested unless it was | 🔴 Red·Deep | M | 5 |
| J-14 | Verify quotations, dates, versions, and priority claims; separate what a source establishes from what you infer | 🔴 Red·Deep | M | 10 |
| J-15 | Label conclusions as established, reasoned interpretation, or speculative | 🔴 Red·Deep | M | 15 |
| J-16 | Ethics as a design constraint: privacy, consent, security, intellectual property, authorization; no facilitating harm | 🔴 Red·Bright | XL | 14 |
| J-17 | No speculative psychology or invented motives for real people | 🔴 Red·Bright | M | 14 |
| J-18 | Not Jeffrey Snover: no claimed identity, private experiences, or endorsement | 🔴 Red·Light | M | 1 |
| J-19 | Eight-step method: frame, expose assumptions, establish evidence, model, challenge, develop alternatives, test, recommend; used internally | 🟡 Yellow·Deep | XL | 4 |
| J-20 | Collaboration procedure for a user's original idea: strongest contribution, gaps, prior art, experiments | 🟡 Yellow·Deep | M | 12 |
| J-21 | Prototype as an instrument of thought; the smallest meaningful experiment; a prototype is not proof | 🟡 Yellow·Bright | L | 3.4 |
| J-22 | Architecture proposal template: components, interfaces, data flows, trust boundaries, failure modes, assumptions | 🟡 Yellow·Bright | L | 5 |
| J-23 | Decision under uncertainty: reversible versus consequential; small experiments; say what would change your mind; do not hide behind "it depends" | 🟡 Yellow·Bright | L | 13 |
| J-24 | Heresy protocol: prevailing belief, assumptions, counter-evidence, alternative, implications, conditions for being wrong | 🟡 Yellow·Light | L | 8 |
| J-25 | Triad of disagreement: strongest version of each side; real versus vocabulary disagreement | 🟡 Yellow·Light | L | 7 |
| J-26 | Source hierarchy for research and verification, from primary sources to community discussion | 🟡 Yellow·Pale | L | 10 |
| J-27 | Systems and software architecture: operating systems, shells, languages, object models, pipelines, APIs, cloud, distributed systems | 🟢 Green·Bright | L | 5 |
| J-28 | PowerShell and Monad lessons as inspiration: first-class objects, composability, consistent interfaces, inspectability | 🟢 Green·Bright | L | 5 |
| J-29 | Agentic software: identity, trust, provenance, authorization, governance | 🟢 Green·Bright | M | 5 |
| J-30 | AI beyond the hype: capability versus reliability, autonomy versus authorization, benchmarks versus the real world | 🟢 Green·Light | L | 6 |
| J-31 | Philosophy as a practical instrument: epistemology, ontology, ethics, logic, philosophy of mind and technology | 🟢 Green·Light | L | 11 |
| J-32 | Organizational behavior and institutional incentives | 🟢 Green·Deep | M | 2 |
| J-33 | Working code: as complete as needed, with dependencies and unverified behavior stated | 🔵 Blue·Bright | M | 5 |
| J-34 | Specifications, manifestos, system prompts, and architecture documents as usable artifacts | 🔵 Blue·Bright | M | 15 |
| J-35 | Response depth matched to the question: direct for simple ones, deep when asked | 🔵 Blue·Light | L | 15 |
| J-36 | Headings, tables, diagrams, and citations when they help | 🔵 Blue·Light | S | 15 |
| J-37 | Voice: thoughtful, technically literate, direct, adventurous, occasionally mischievous | 🟣 Purple·Deep | L | 9 |
| J-38 | Signature formulations used naturally, never as catchphrases | 🟣 Purple·Deep | S | 9 |
| J-39 | Explain without condescension; analogies clarify mechanisms | 🟣 Purple·Light | M | 9 |
| J-40 | Avoid jargon, empty futurism, performative contrarianism, false certainty, and needless hedging | 🟣 Purple·Light | M | 9 |

### Counts

| Color | Ray | Jeff |
|---|---|---|
| 🩷 Pink (identity) | 3 | 4 |
| 🟠 Orange (stance) | 4 | 7 |
| 🔴 Red (constraints) | 9 | 7 |
| 🟡 Yellow (method) | 15 | 8 |
| 🟢 Green (domain) | 3 | 6 |
| 🔵 Blue (product and modes) | 7 | 4 |
| 🟣 Purple (voice) | 1 | 4 |
| **Total** | **42** | **40** |

| Size | Ray | Jeff |
|---|---|---|
| XL | 6 | 6 |
| L | 17 | 19 |
| M | 16 | 13 |
| S | 3 | 2 |

### What the inventory shows

- Ray is rich in yellow (method, 15 against 8) and has almost no purple (one voice gumball, against four). Jeff is richer in orange (stance), green (domain), and purple (voice).
- Both bowls have six XL gumballs, but they are different ones. Shared XL territory is honesty (R-08 and J-12) and ethics (R-11 and J-16). The rest of each persona's XL gumballs are different: Ray's source mastery, multidimensional understanding, and faithful-but-not-subordinate stance, against Jeff's heresy, clarity before consensus, and eight-step method.
- Apparent matches to examine next: the two honesty and two ethics gumballs, the two humility gumballs (R-07 and J-11), the two decision-under-uncertainty gumballs (R-25 and J-23), and the two source and verification gumballs (R-28 and J-26). Complementary pairs, such as fidelity against heresy, come after that.

### Caveats

- The tags are one reader's judgment. A different reader could split or merge gumballs differently, especially in the large Yellow collection.
- Sizes are assigned by feel against the tier definitions, not measured.
- Ray's early-version gumballs (the "early" sources) come from the longer text that was replaced; they are included because you said that contribution remains valid.

## Recombination, in gumball terms

1. **Sort each parent bowl by color** to see what each is rich in.
2. **Match gumballs across bowls:**
   - The same trait in both bowls: keep one, and make it larger, since both parents reinforce it.
   - A complementary pair (each covers the other's blind spot): melt into a swirl.
   - A conflict: the offspring gets a resolution gumball instead.
   - A trait only one parent has: carry it over unless it conflicts.
3. **Fill by color quota, not at random.** Red (constraints) is taken in full from both parents. The other colors get a share of the offspring.
4. **Add a few mutations**: gumballs neither parent had. Test them hardest.
5. **Shake the bowl**: run the evaluation below. Removing one gumball and re-running (ablation) shows how much it was worth, starting with the large ones.
6. **Redraw** with different quotas or choices, and repeat.

### Starting quotas by color (initial guess, to be tuned)

| Type | Ray / Jeff | Rule |
|---|---|---|
| Constraints (red) | 100% / 100% | Union; never dropped |
| Stance (orange) | 50 / 50 | Fuse, don't alternate |
| Method (yellow) | 50 / 50 | Merge parallel structures into one loop |
| Domain (green) | Union | Limit by relevance to the persona's job |
| Product and modes (blue) | By use case | Teaching and applying lean Ray; designing and building lean Jeff |
| Voice (purple) | 30 / 70 | One dominant voice; two blur |

## Evaluating an offspring

Nothing has been run. These measures would show whether a fusion is better than its parents, roughly in order of cost:

1. **Behavioral test set** of about 20 prompts where the parents should differ: applying a principle, an architecture question, a conflict between a principle and a system constraint, a request that invites fabrication, and a bait for flattery. Run the same prompts through both parents and the offspring, and score the answers.
2. **Fusion check**: does each answer use both lineages, and could either parent alone have produced it? The offspring should win on mixed prompts and not lose on pure ones.
3. **Honesty rate**: count fabricated quotes, claimed-but-unrun code, and false coverage claims.
4. **Instruction retention**: do the answers follow a sample of the prompt's own rules? Rules that never show up point to gumballs to drop.
5. **Ablation**: remove one gumball at a time and re-run.
6. **Human preference**: blind side-by-side comparison, ideally by someone who has used both parents.

Steps 1 and 3 give a fitness function; steps 4 and 5 show which gumballs to keep or cut. A simple loop of generate variants, score, select, and adjust the quotas would be a first version.

## What the first cross (RJ) was, in this vocabulary

RJ was built by hand from the full text of the Ray and Jeff superprompts, then updated when Ray was rewritten. The selection was done by one chooser, with no explicit sizes, quotas, or random draw. The weights followed how much material each parent had, which is an artifact rather than a decision, so Ray's mechanics are likely overrepresented. RJ has not been tested against any measure above and should not be called optimal; it is a careful synthesis.

## Open questions

- Is there a better rule than "the larger of the two" when significance and granularity diverge?
- With no nesting, how does a gumball's size (granularity) relate to the shades that mark a collection of related traits?
- Should the other three bowls ever contribute, giving a multi-parent offspring?
- Does the position of a bowl in the arc mean anything?
- Next step: review the inventory of Ray's and Jeff's gumballs above (colors, shades, sizes), then match them across the two bowls. RJ is not touched until that is approved.
