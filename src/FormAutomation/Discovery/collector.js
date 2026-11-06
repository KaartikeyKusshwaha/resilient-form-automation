return (() => {
  const selector = [
    'input', 'select', 'textarea', '[role="textbox"]', '[role="radio"]',
    '[role="radiogroup"]', '[role="combobox"]', '[role="listbox"]',
    '[aria-haspopup="listbox"]', '[contenteditable="true"]'
  ].join(',');
  const clean = value => String(value || '').replace(/\s+/g, ' ').trim();
  const textOf = node => clean(node ? (node.innerText ?? node.textContent ?? '') : '');
  const rootIds = new Map();
  const rootKey = root => {
    if (!(root instanceof ShadowRoot)) return 'main';
    if (!rootIds.has(root)) rootIds.set(root, `shadow:${rootIds.size}`);
    return rootIds.get(root);
  };
  const parentOrHost = node => node?.parentElement ||
    (node?.getRootNode?.() instanceof ShadowRoot ? node.getRootNode().host : null);
  const controls = [];
  const frames = [];
  const roots = [document];
  while (roots.length) {
    const root = roots.shift();
    let descendants = [];
    try { descendants = [...root.querySelectorAll('*')]; } catch { continue; }
    for (const host of descendants) {
      try {
        if (host.shadowRoot?.mode === 'open') roots.push(host.shadowRoot);
        if (host.tagName === 'IFRAME' || host.tagName === 'FRAME') frames.push(host);
      } catch { /* Ignore detached or cross-realm nodes. */ }
    }
    try {
      for (const element of root.querySelectorAll(selector)) {
        try { controls.push({ element, root }); } catch { /* One bad element never aborts collection. */ }
      }
    } catch { /* Ignore an inaccessible tree. */ }
  }

  const candidateControls = controls.map(item => item.element);
  const scopeIds = new WeakMap();
  const groupIds = new WeakMap();
  let nextScope = 0, nextGroup = 0;
  const idFor = (map, element, prefix, next) => {
    if (!element) return `${prefix}:root`;
    if (!map.has(element)) map.set(element, `${prefix}:${next()}`);
    return map.get(element);
  };
  const countInside = (container, root) => candidateControls.filter(control =>
    control.getRootNode() === root && (container === root || container.contains?.(control))).length;
  const scopeFor = (element, root) => {
    let form = null;
    try { form = element.closest('form'); } catch { /* custom roots */ }
    if (form) return { node: form, key: idFor(scopeIds, form, 'form', () => ++nextScope) };
    let node = element;
    for (let level = 0; node && node !== root && level < 6; level++, node = node.parentElement) {
      if (countInside(node, root) >= 3)
        return { node, key: idFor(scopeIds, node, 'scope', () => ++nextScope) };
    }
    return { node: root, key: idFor(scopeIds, root, 'root', () => ++nextScope) };
  };
  const radioLike = element => element.matches('input[type="radio"],[role="radio"]');
  const radioPeers = (element, root, scope) => {
    const name = element.getAttribute('name');
    if (name) {
      const peers = candidateControls.filter(other => radioLike(other) && other.getRootNode() === root &&
        other.getAttribute('name') === name && scopeFor(other, root).key === scope.key);
      if (peers.length >= 2) return peers;
    }
    let parent = element.parentElement;
    for (let level = 0; parent && parent !== root && level < 5; level++, parent = parent.parentElement) {
      const peers = candidateControls.filter(other => radioLike(other) && other.getRootNode() === root && parent.contains(other));
      if (peers.length >= 2) return peers;
    }
    return [element];
  };
  const visible = element => {
    const rect = element.getBoundingClientRect();
    let hidden = false, opacityZero = false, ariaHidden = false;
    for (let node = element; node; node = parentOrHost(node)) {
      const style = getComputedStyle(node);
      hidden ||= style.display === 'none' || style.visibility === 'hidden' || !!node.hidden;
      opacityZero ||= Number.parseFloat(style.opacity) === 0;
      ariaHidden ||= node.getAttribute?.('aria-hidden') === 'true';
    }
    const offscreen = rect.right + scrollX <= 0 || rect.bottom + scrollY <= 0;
    const tinyBox = rect.width * rect.height < 4;
    return { selfVisible: !hidden && !opacityZero && !offscreen && !tinyBox,
      opacityZero, ariaHidden, offscreen, tinyBox, rect };
  };
  const labelTexts = element => {
    const texts = [];
    try { for (const label of element.labels || []) texts.push(textOf(label)); } catch { /* custom role */ }
    try { const wrapper = element.closest('label'); if (wrapper) texts.push(textOf(wrapper)); } catch { /* shadow host */ }
    return [...new Set(texts.filter(Boolean))];
  };
  const labelledByTexts = (element, root) => {
    const ids = clean(element.getAttribute('aria-labelledby')).split(' ').filter(Boolean);
    return ids.map(id => { try { return textOf(root.getElementById?.(id)); } catch { return ''; } }).filter(Boolean);
  };
  const safeText = element => {
    try { return textOf(element); } catch { return ''; }
  };
  const textWithoutControls = node => {
    try {
      const clone = node.cloneNode(true);
      clone.querySelectorAll(selector).forEach(control => control.remove());
      return textOf(clone);
    } catch { return safeText(node); }
  };
  const siblingText = element => {
    let sibling = element.nextSibling;
    let value = '';
    while (sibling && value.length < 40) {
      if (sibling.nodeType === Node.ELEMENT_NODE) {
        if (sibling.matches?.(selector) || sibling.tagName === 'BR' || sibling.tagName === 'LABEL') break;
        value += ` ${textWithoutControls(sibling)}`;
      } else if (sibling.nodeType === Node.TEXT_NODE) value += ` ${sibling.textContent || ''}`;
      value = clean(value);
      sibling = sibling.nextSibling;
    }
    return value.slice(0, 40);
  };
  const groupAnchor = element => {
    let node = element;
    for (let level = 0; node && level < 5; level++, node = node.parentElement) {
      if (node.matches?.('fieldset,[role="radiogroup"]')) return node;
    }
    return element.parentElement || element;
  };
  const nearby = (element, anchor, root) => {
    const anchorRect = anchor.getBoundingClientRect();
    let nearLeft = '', nearAbove = '';
    let ancestor = anchor.parentElement;
    for (let level = 0; ancestor && ancestor !== root && level < 4; level++, ancestor = ancestor.parentElement) {
      const candidates = [...ancestor.querySelectorAll('label,span,div,p,td,th,legend,b,strong,em,dt,h1,h2,h3,h4,h5,h6,li')]
        .filter(node => !node.querySelector(selector) && node !== anchor && !anchor.contains(node))
        .map(node => ({ node, text: textOf(node), rect: node.getBoundingClientRect() }))
        .filter(item => item.text.length > 0 && item.text.length <= 60);
      const left = candidates.filter(item => item.rect.right <= anchorRect.left + 4 &&
          Math.min(item.rect.bottom, anchorRect.bottom) - Math.max(item.rect.top, anchorRect.top) >= anchorRect.height * 0.4 &&
          anchorRect.left - item.rect.right <= 400)
        .sort((a, b) => (anchorRect.left - a.rect.right) - (anchorRect.left - b.rect.right))[0];
      const above = candidates.filter(item => item.rect.bottom <= anchorRect.top + 4 &&
          Math.min(item.rect.right, anchorRect.right) - Math.max(item.rect.left, anchorRect.left) >= 1 &&
          anchorRect.top - item.rect.bottom <= 60)
        .sort((a, b) => (anchorRect.top - a.rect.bottom) - (anchorRect.top - b.rect.bottom))[0];
      if (!nearLeft && left) nearLeft = left.text;
      if (!nearAbove && above) nearAbove = above.text;
      if (nearLeft || nearAbove) break;
    }
    let precedingSiblingText = '';
    let node = element;
    for (let level = 0; node && level < 3 && !precedingSiblingText; level++, node = node.parentElement) {
      const previous = node.previousElementSibling;
      if (previous) {
        const value = textOf(previous);
        if (value.length > 0 && value.length <= 60) precedingSiblingText = value;
      }
    }
    return { nearLeft, nearAbove, precedingSiblingText };
  };

  const descriptors = [];
  for (let index = 0; index < controls.length; index++) {
    try {
      const { element, root } = controls[index];
      const tag = element.tagName.toLowerCase();
      const type = clean(element.getAttribute('type')).toLowerCase();
      const role = clean(element.getAttribute('role')).toLowerCase();
      let kind = '';
      if (type === 'radio' || role === 'radio' || role === 'radiogroup') kind = 'radio';
      else if (tag === 'select' || role === 'combobox' || role === 'listbox' || element.getAttribute('aria-haspopup') === 'listbox') kind = 'select';
      else if (tag === 'textarea' || role === 'textbox' || element.isContentEditable || (tag === 'input' && ['','text','search'].includes(type))) kind = 'text';
      if (!kind) continue;
      const labels = labelTexts(element);
      const labelRefs = labelledByTexts(element, root);
      const labelled = [...labels, ...labelRefs];
      const fieldset = element.closest('fieldset');
      const legendText = textOf(fieldset?.querySelector('legend'));
      const radioGroup = element.closest('[role="radiogroup"]');
      const groupAriaLabel = clean(radioGroup?.getAttribute('aria-label')) || textOf(root.getElementById?.(radioGroup?.getAttribute('aria-labelledby')));
      const scope = scopeFor(element, root);
      const info = visible(element);
      const labelVisible = labels.some(label => {
        try { const node = (element.labels || []).find(item => textOf(item) === label); return node ? visible(node).selfVisible : false; }
        catch { return false; }
      });
      const anchor = kind === 'radio' ? groupAnchor(element) : element;
      const near = nearby(element, anchor, root);
      const modal = element.closest('[role="dialog"],dialog,[aria-modal="true"]');
      const options = tag === 'select' ? [...element.options].slice(0, 500).map(option => ({
        text: textOf(option), value: option.value || '', disabled: !!option.disabled, selected: !!option.selected
      })) : [];
      const groupPeers = kind === 'radio' ? radioPeers(element, root, scope) : [];
      let groupKey = '';
      if (kind === 'radio') {
        const named = element.getAttribute('name');
        const groupNode = named && groupPeers.length >= 2 ? scope.node : groupAnchor(element);
        groupKey = idFor(groupIds, groupNode, 'group', () => ++nextGroup);
      }
      const ancestorHidden = info.ariaHidden && !labelVisible;
      const ownLabel = clean(labelled.join(' '));
      descriptors.push({ index, tag, type, role, id: element.id || '', name: element.getAttribute('name') || '',
        classes: typeof element.className === 'string' ? element.className : '', value: element.value ?? element.getAttribute('value') ?? '',
        checked: !!element.checked || !!element.getAttribute('aria-checked') && element.getAttribute('aria-checked') === 'true',
        disabled: !!element.disabled || element.getAttribute('aria-disabled') === 'true', readOnly: !!element.readOnly,
        required: !!element.required || element.getAttribute('aria-required') === 'true', selfVisible: info.selfVisible,
        labelVisible, offscreen: info.offscreen, opacityZero: info.opacityZero, ariaHidden: ancestorHidden, tinyBox: info.tinyBox,
        labelTexts: labels, labelledByTexts: labelRefs, ariaLabel: element.getAttribute('aria-label') || '',
        placeholder: element.getAttribute('placeholder') || '', title: element.getAttribute('title') || '', legendText,
        groupAriaLabel, nearLeft: near.nearLeft, nearAbove: near.nearAbove,
        precedingSiblingText: near.precedingSiblingText, nextText: kind === 'radio' ? siblingText(element) : '', options,
        rect: { x: info.rect.x, y: info.rect.y, w: info.rect.width, h: info.rect.height }, rootKey: rootKey(root),
        scopeKey: scope.key, scopeControlCount: countInside(scope.node, root), groupKey, inModal: !!modal,
        formKey: scope.key, order: index, kind, label: ownLabel, accessible: clean([element.getAttribute('aria-label'), element.getAttribute('placeholder'), element.getAttribute('title'), ...labelRefs].join(' ')),
        nearby: clean([near.nearLeft, near.nearAbove, near.precedingSiblingText, legendText, groupAriaLabel].join(' ')),
        attributes: clean([element.id, element.getAttribute('name'), element.className, element.getAttribute('data-testid')].join(' ')),
        optionText: kind === 'radio' ? (siblingText(element) || labels[0] || element.getAttribute('value') || '') : ''
      });
    } catch { /* Never fail the entire snapshot because one custom element is unusual. */ }
  }
  return [controls.filter(item => descriptors.some(d => d.index === controls.indexOf(item))).map(item => item.element), descriptors, frames];
})();
