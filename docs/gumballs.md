# Gumballs: recombining persona traits

How to select and recombine the inheritable traits of persona superprompts (for example, Ray and Jeff into RJ), using a gumball-and-bowls picture from a Digitomic Evolution perspective. This replaces the earlier working term "atoms".

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

A proposed mapping. The first image shows eight colors, and a ninth (teal) appears in the second.

| Color | Type | Example |
|---|---|---|
| Red | Constraints (hard rules) | No fabricated sources or coverage |
| Orange | Stance | Independence from the source's author |
| Yellow | Method | The five-step loop, the Heresy Protocol |
| Green | Domain | Decision theory, systems architecture |
| Blue | Product (what gets made) | Lessons, specifications, prototypes |
| Light blue | Voice | Dry, direct, allergic to jargon |
| Purple | Modes | TEACH, DIAGNOSE, RESOLVE |
| Pink | Identity | "I am not Dalio, I am not Snover" |
| Teal | Open | Perhaps source handling, or self-improvement |

## Size: significance and granularity together

A large gumball is a significant, coarse-grained trait that defines a lot of the persona's behavior. A small gumball is a fine-grained, minor trait.

- **Gumballs do not nest.** A gumball never contains other gumballs. Each is a single asset in the bowl.
- **When significance and granularity diverge**, such as a single short sentence that is critical to the persona, size by the larger of the two so the trait stays large.
- **Heritability is separate from size.** Whether a gumball passes to the offspring is a selection decision, not a property of its size.
- **In the images** each bowl has a few large gumballs of different colors, which is each parent's signature of defining traits, plus a middle range and many small ones.

## Shade: related traits

Related traits, or a collection of traits that belong together, are shown by shades of one color family. For example, the red constraint gumballs about not fabricating (sources, quotations, code, coverage, learning claims) would be several shades of red. This is a proposal, and the exact scheme is still open.

- A family of shades can be taken together, which keeps a collection coherent, or only in part, which is riskier because the collection may stop working when some of it is missing.
- Shade is separate from color (the kind of trait) and from size (significance and granularity).

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
| Product (blue) | By use case | Teaching and applying lean Ray; designing and building lean Jeff |
| Voice (light blue) | 30 / 70 | One dominant voice; two blur |

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

- What does the ninth, teal color represent?
- Is there a better rule than "the larger of the two" when significance and granularity diverge?
- With no nesting, how does a gumball's size (granularity) relate to the shades that mark a collection of related traits?
- Should the other three bowls ever contribute, giving a multi-parent offspring?
- Does the position of a bowl in the arc mean anything?
- Next step: tag each of Ray's and Jeff's traits with a color, a size, and the smaller gumballs inside each large one, then review the list before using it for recombination.
