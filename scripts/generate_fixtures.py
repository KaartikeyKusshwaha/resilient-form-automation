"""Create deterministic, oracle-marked mutation pages for the Task 1 resolver tests."""

from __future__ import annotations

import html
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "fixtures" / "mutations"


def normal_form(order: tuple[str, ...] = ("first", "last", "gender", "state", "country"), scrambled: bool = False) -> str:
    ids = {key: f"q{index:02d}" if scrambled else key for index, key in enumerate(("first", "last", "male", "female", "trans", "state", "country"), 41)}
    names = {key: f"n{index:02x}" if scrambled else key for index, key in enumerate(ids, 12)}
    classes = "class='x91'" if scrambled else "class='field'"
    rows = {
        "first": f"<div {classes}><label for='{ids['first']}'>First Name</label><input id='{ids['first']}' name='{names['first']}' type='text' data-test-target='first_name'></div>",
        "last": f"<div {classes}><label for='{ids['last']}'>Last Name</label><input id='{ids['last']}' name='{names['last']}' type='text' data-test-decoy></div>",
        "gender": f"<fieldset {classes}><legend>Gender</legend><label><input id='{ids['male']}' name='{names['male']}' type='radio' value='Male' data-test-target='gender_male'> Male</label><label><input id='{ids['female']}' name='{names['female']}' type='radio' value='Female'> Female</label><label><input id='{ids['trans']}' name='{names['trans']}' type='radio' value='Transgender'> Transgender</label></fieldset>",
        "state": f"<div {classes}><label for='{ids['state']}'>State</label><select id='{ids['state']}' name='{names['state']}' data-test-target='state'><option value=''>-- Select State --</option><option>Gujarat</option><option>California</option></select></div>",
        "country": f"<div {classes}><label for='{ids['country']}'>Country</label><select id='{ids['country']}' name='{names['country']}' data-test-decoy><option value=''>-- Select Country --</option><option>India</option><option>United States</option></select></div>",
    }
    content = "".join(rows[key] for key in order)
    return f"""<!doctype html><html><head><meta charset='utf-8'><title>Form mutation</title><style>body{{font:16px Arial;margin:30px}}.field{{display:block;margin:12px;padding:6px}}fieldset{{margin:12px}}</style></head><body><form>{content}<button type='button'>Submit</button></form></body></html>"""


def no_labels() -> str:
    return """<!doctype html><html><body><section><input type='text' placeholder='Your given name' data-test-target='first_name'><div role='radiogroup' aria-label='Gender'><input type='radio' name='r' data-test-target='gender_male'><span>Male</span><input type='radio' name='r'><span>Female</span><input type='radio' name='r'><span>Transgender</span></div><select aria-label='State' data-test-target='state'><option>Choose state</option><option>Gujarat</option><option>California</option></select></section></body></html>"""


def custom_controls() -> str:
    return """<!doctype html><html><head><style>body{font:16px Arial}.radio{display:inline-block;padding:8px}input[type=radio]{opacity:0;width:1px;height:1px;position:absolute}</style></head><body><form><label>First Name <input type='text' data-test-target='first_name'></label><fieldset><legend>Gender</legend><label class='radio'><input type='radio' name='g' value='Male' data-test-target='gender_male'>Male</label><label class='radio'><input type='radio' name='g' value='Female'>Female</label></fieldset><div><span>State</span><button type='button' role='combobox' aria-label='State' aria-haspopup='listbox' data-test-target='state'>Select a state</button><ul role='listbox' style='display:none'><li role='option'>Gujarat</li><li role='option'>California</li></ul></div></form><script>const button=document.querySelector('[role=combobox]'),list=document.querySelector('[role=listbox]');button.addEventListener('click',()=>list.style.display='block');for(const option of list.children)option.addEventListener('click',()=>{button.textContent=option.textContent;list.style.display='none';option.setAttribute('aria-selected','true')});</script></body></html>"""


def late_render() -> str:
    form = normal_form().split("<body>", 1)[1].split("</body>", 1)[0]
    return f"<!doctype html><html><body><main id='mount'>Loading form...</main><script>setTimeout(()=>document.querySelector('#mount').innerHTML={form!r},350)</script></body></html>"


def iframe() -> str:
    inner = html.escape(normal_form(), quote=True)
    return f"<!doctype html><html><body><h1>Outer page</h1><iframe title='form frame' srcdoc=\"{inner}\"></iframe></body></html>"


def shadow() -> str:
    markup = normal_form().split("<body>", 1)[1].split("</body>", 1)[0].replace("`", "\\`")
    return f"<!doctype html><html><body><main id='host'></main><script>document.querySelector('#host').attachShadow({{mode:'open'}}).innerHTML=`{markup}`;</script></body></html>"


def shadow_iframe() -> str:
    inner = normal_form().replace("`", "\\`").replace("${", "\\${")
    return f"<!doctype html><html><body><main id='host'></main><script>const r=document.querySelector('#host').attachShadow({{mode:'open'}});const f=document.createElement('iframe');f.title='nested form';f.srcdoc=`{inner}`;r.appendChild(f);</script></body></html>"


def decoys() -> str:
    good = normal_form().split("<body>", 1)[1].split("</body>", 1)[0]
    decoys = """<div><label>Middle Name<input type='text' data-test-decoy></label><label>Nickname<input type='text' data-test-decoy></label><label>Emergency Contact First Name<input type='text' data-test-decoy></label><input name='first_name' style='display:none' aria-label='First Name' data-test-decoy><label style='position:absolute;left:-9999px'>First Name<input type='text' data-test-decoy></label><fieldset><legend>Newsletter</legend><label><input type='radio' name='news'>Yes</label><label><input type='radio' name='news'>No</label></fieldset><label>Birth State<select data-test-decoy><option>Choose</option><option>Gujarat</option></select></label><label>State of residence<select data-test-decoy><option>Choose</option><option>Gujarat</option></select></label><label>Country<select data-test-decoy><option>India</option></select></label><label>City<select data-test-decoy><option>Ahmedabad</option></select></label><dialog style='display:none'><label>First Name<input type='text' data-test-decoy></label><label>State<select data-test-decoy></select></label></dialog></div>"""
    return f"<!doctype html><html><body><form>{decoys}{good}</form></body></html>"


def restructured() -> str:
    return """<!doctype html><html><head><style>.row{margin:12px;padding:8px}.caption{display:inline-block;width:150px}</style></head><body><div class='deep'><div><div><div><section><div class='row'><span class='caption'>First Name</span><input type='text' data-test-target='first_name'></div><div class='row'><span class='caption'>Last Name</span><input type='text' data-test-decoy></div><div class='row'><span class='caption'>Gender</span><input type='radio' name='x' value='Male' data-test-target='gender_male'><span>Male</span><input type='radio' name='x' value='Female'><span>Female</span></div><div class='row'><span class='caption'>State</span><select data-test-target='state'><option>Select</option><option>Gujarat</option><option>California</option></select></div></section></div></div></div></div></body></html>"""


def two_forms() -> str:
    fragment = normal_form().split("<body>", 1)[1].split("</body>", 1)[0]
    fragment = fragment.removeprefix("<form>").removesuffix("</form>")
    decoy = fragment.replace("data-test-target=", "data-test-decoy=")
    # The first form is deliberately compact and weak; the larger second form should win the form-scope prior.
    primary = fragment
    for key in ("first", "last", "male", "female", "trans", "state", "country"):
        primary = primary.replace(f"id='{key}'", f"id='primary_{key}'").replace(f"for='{key}'", f"for='primary_{key}'")
    extra = "".join(f"<label>Extra field {index}<input type='text'></label>" for index in range(8))
    return f"<!doctype html><html><body><form>{decoy}</form><form>{extra}{primary}</form></body></html>"


def randomized() -> str:
    return normal_form(("country", "state", "last", "gender", "first"), scrambled=True).replace("First Name", "Given Name").replace("Gender", "Sex").replace("State", "Province")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    pages = {
        "m00_baseline.html": normal_form(),
        "m01_reordered.html": normal_form(("state", "gender", "last", "first", "country")),
        "m02_attr_scramble.html": normal_form(scrambled=True),
        "m03_structure.html": restructured(),
        "m04_decoys.html": decoys(),
        "m05_no_labels.html": no_labels(),
        "m06_custom_controls.html": custom_controls(),
        "m07_late_render.html": late_render(),
        "m08_iframe.html": iframe(),
        "m09_shadow.html": shadow(),
        "m10_shadow_iframe.html": shadow_iframe(),
        "m11_two_forms.html": two_forms(),
        "m12_randomized.html": randomized(),
    }
    for name, html in pages.items():
        (OUT / name).write_text(html, encoding="utf-8")
    print(f"Wrote {len(pages)} mutation fixtures to {OUT}")


if __name__ == "__main__":
    main()
