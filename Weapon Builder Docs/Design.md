# Idea:
To build a silly little modular weapon building tool sorta like the spore creature creator. Each part will have "sockets" that allow for the attaching of other parts at specific orientations. (e.g. a Handle that has a socket that can take a Crossguard, Pommel, or Blade. A large Handle might have 2 or 3 sockets on it). 

I would imagine it would work best of each socket had some sort of normalization of size so things don't look too unnatural. (e.g a greatsword with a spearhead on it would probably have a rather large spearhead. Likewise a mace with longsword shaped flanges probably doesn't leave the flanges full sized, at least usually)

Each socket would need the potential to support more than one type in component. Components have types associated with them (classes or interfaces)

In theory, the weapon construction interface would look like Spore's creature creator with a base Handle and various Sockets on the Handle. Tabs for switching between types of components exist and are filled with component options. They are dragged from the component box into the 3D space and float with your cursor until nearby a socked whereby they snap to the socket. The app would support rotating the camera around the weapon, removing components by dragging them off the socket and swapping out the components by dragging another component to an already occupied socket. There would also be various textures that could be applied to the components, corresponding to different materials. When the final weapon is in a finished state, the user clicks build and the new weapon is saved to a gallery. It would also support a randomization feature to allow a random weapon to be constructed with the click of a button. 
## Component Modifiers
Maybe other modifiers can be applied to certain components, such as a blade having varying types of cutting edges, modeled differently (e.g. jagged, sharp, battered, sawed, bleeders)
Achieved by fitting an Edge mesh to the shape of the blade.

These could be extended to other "business end" components or perhaps any component if there's enough design space.

## Tech Stack
C# and Godot

## Other Thoughts
Stretch goal: This would actually make a pretty funny game, where you fight enemies, pick up their weapons and armor and throw it into a vat to melt it all down and build new, increasingly ridiculous weapons with the scrap. Especially if each component had different effects to stack together. Then over time you accumulate more molds and blueprints. Maybe from a gacha style pack opening.

Of course this would mean more components added more weight, heavier weapons are slower, hit harder, that kinda thing. Different handles support different movesets. A polearm with mace head behaves differently than a dagger with an axe head, presumably adopting the attack style of the handle wielded. Components made with heavier metals have different modifiers and change their weight.
## Decisions (2026-10-24)
- **Engine:** Godot 4.7 (.NET edition) with **C#**. Not godot-jvm/Kotlin.
- **Standalone project.** Code from The Mountain may be ported where it saves work (MeshKit's `flat_mesh`, object-space triplanar materials, the item outline shader).
- **Sockets accept tags, not classes.** A socket lists the tags it takes; a part lists the tags it has. A part fits if they share one.
- **A socket is data:** position, rotation, accepted tags, a size budget (length, width), whether it's required, and an optional rotation step.
- **Scaling:** a part's scale is `min(socket.length / part.length, socket.width / part.width)`, kept between 0.25 and 4. Child sockets inherit it.
- **Edges:** each blade stores its edge as a curve; an edge type is a cross-section (or a repeating piece) swept along it.
- **Materials:** object-space triplanar, so no UVs are needed and textures move with the weapon.
- **Snapping:** while dragging, compatible sockets light up, the part snaps to the nearest one within a radius on screen, and a see-through preview shows where it'll go.
- **Randomize:** a random walk down the tree: fill required sockets, fill optional ones by chance, cap the depth, and pick materials.

## Plans
- [[2026-10-24-mvp-builder|MVP builder]]: snap, swap, remove, orbit, save to the gallery. **← current**
- Later: materials, edges, randomize, gallery browser, part icons.
