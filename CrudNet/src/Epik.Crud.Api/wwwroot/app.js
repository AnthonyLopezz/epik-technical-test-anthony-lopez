const API = '/api/personas';
const PAGE_SIZE = 10;
const $ = id => document.getElementById(id);
const state = { page: 1, filter: '', searchId: null, current: null };

const notyf = new Notyf({
  duration: 3500, position: { x: 'right', y: 'top' }, dismissible: true, ripple: false,
  types: [{ type: 'success', background: '#1f6f43' }, { type: 'error', background: '#b3261e' }]
});
const escapeHtml = s => String(s).replace(/[&<>"']/g, c => '&#' + c.charCodeAt(0) + ';');
const toast = {
  success: text => notyf.success(escapeHtml(text)),
  error: text => notyf.error(escapeHtml(text))
};

const FILTERS = { digits: /[^0-9]/g, letters: /[^\p{L} '-]/gu };
const FORMATS = {
  digits: [/^[0-9]+$/, 'Solo se permiten números.'],
  letters: [/^\p{L}+(?:[ '-]+\p{L}+)*$/u, 'Solo se permiten letras.']
};

const capitalize = v => v.normalize('NFC').trim().replace(/ {2,}/g, ' ').toLowerCase()
  .replace(/(?<=^|[ '-])\p{L}/gu, l => l.toUpperCase());

function fieldError(el) {
  const v = el.value.trim();
  if (el.validity.badInput) return 'Debe ser un número entero.';
  if (!v) return 'Este campo es obligatorio.';
  if (el.maxLength > 0 && v.length > el.maxLength) return 'Máximo ' + el.maxLength + ' caracteres.';
  const format = FORMATS[el.dataset.format];
  if (format && !format[0].test(v)) return format[1];
  if (el.type === 'number') {
    const n = Number(v);
    if (!Number.isInteger(n)) return 'Debe ser un número entero.';
    if (n < Number(el.min) || n > Number(el.max)) return 'Debe estar entre ' + el.min + ' y ' + el.max + '.';
  }
  return '';
}

function validateField(el) {
  const id = 'err-' + el.form.id + '-' + el.name;
  let hint = document.getElementById(id);
  if (!hint) {
    hint = document.createElement('small');
    hint.id = id; hint.className = 'field-error';
    el.after(hint);
    el.setAttribute('aria-describedby', id);
  }
  hint.textContent = fieldError(el);
  el.setAttribute('aria-invalid', hint.textContent !== '');
  return !hint.textContent;
}

function normalize(el) {
  if (el.dataset.format === 'letters') el.value = capitalize(el.value);
}

function validateForm(form) {
  const fields = [...form.querySelectorAll('input, select')];
  fields.forEach(normalize);
  const invalid = fields.filter(el => !validateField(el));
  invalid[0]?.focus();
  return !invalid.length;
}

function clearErrors(form) {
  form.querySelectorAll('.field-error').forEach(e => { e.textContent = ''; });
  form.querySelectorAll('[aria-invalid]').forEach(e => e.removeAttribute('aria-invalid'));
}

for (const form of document.forms) {
  form.noValidate = true;
  form.addEventListener('focusout', e => {
    const el = e.target;
    if (!el.name || el.form.id === 'search') return;
    normalize(el);
    if (el.value || el.validity.badInput) validateField(el);
  });
  form.addEventListener('keydown', e => {
    if (e.target.type === 'number' && ['e', 'E', '+', '-', '.', ','].includes(e.key)) e.preventDefault();
  });
  form.addEventListener('input', e => {
    const el = e.target, filter = FILTERS[el.dataset.format];
    if (filter) {
      const clean = el.value.normalize('NFC').replace(filter, '');
      if (clean !== el.value) {
        const cursor = Math.max(0, el.selectionStart - (el.value.length - clean.length));
        el.value = clean;
        el.setSelectionRange(cursor, cursor);
      }
    }
    if (el.getAttribute('aria-invalid') === 'true') validateField(el);
  });
}

// --- API: every response is { success, message, data, pagination? } ---
async function request(url, options = {}) {
  const res = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...options });
  const body = await res.json().catch(() => null);
  if (res.ok && body?.success) return body;
  throw new Error(body?.message || 'Error inesperado (' + res.status + ').');
}

async function busy(form, action) {
  const button = form.querySelector('button:not([type="button"])');
  button.disabled = true;
  try { await action(); } finally { button.disabled = false; }
}

// --- Table ---
function cell(row, label, value, className) {
  const td = row.insertCell();
  td.textContent = value;
  td.dataset.label = label;
  if (className) td.className = className;
}

function actionButton(text, label, className, onClick) {
  const b = document.createElement('button');
  b.type = 'button'; b.className = className; b.textContent = text;
  b.setAttribute('aria-label', label);
  b.addEventListener('click', onClick);
  return b;
}

const fullName = p => p.nombres + ' ' + p.apellidos;

function renderRows(people, emptyText) {
  const tbody = $('rows');
  tbody.replaceChildren();
  if (!people.length) {
    const td = tbody.insertRow().insertCell();
    td.colSpan = 5; td.className = 'empty'; td.textContent = emptyText;
    return;
  }
  for (const p of people) {
    const row = tbody.insertRow();
    cell(row, 'Identificación', p.identificacion, 'id');
    cell(row, 'Nombre', fullName(p), 'name');
    cell(row, 'Edad', p.edad, 'num age');
    cell(row, 'Género', p.genero, 'gender');
    const actions = row.insertCell();
    actions.className = 'actions-cell';
    actions.append(
      actionButton('Editar edad', 'Editar edad de ' + fullName(p), 'btn small', () => openAge(p)),
      actionButton('Eliminar', 'Eliminar a ' + fullName(p), 'btn small danger-text', () => openDelete(p)));
  }
}

async function loadList() {
  state.searchId = null;
  $('search-banner').hidden = true;
  $('pager').hidden = false;
  try {
    const { data, pagination: p } = await request(API + state.filter + '?page=' + state.page + '&pageSize=' + PAGE_SIZE);
    // The last record of the last page was deleted: step back.
    if (!data.length && state.page > 1) { state.page = Math.max(1, p.totalPages); return loadList(); }
    const noun = state.filter ? (p.totalItems === 1 ? 'mujer registrada' : 'mujeres registradas')
      : (p.totalItems === 1 ? 'persona registrada' : 'personas registradas');
    $('summary').textContent = p.totalItems + ' ' + noun;
    renderRows(data, state.filter ? 'No hay mujeres registradas.' : 'Aún no hay personas registradas.');
    $('page-info').textContent = 'Página ' + p.page + ' de ' + Math.max(1, p.totalPages);
    $('prev').disabled = p.page <= 1;
    $('next').disabled = p.page >= p.totalPages;
  } catch (err) {
    renderRows([], 'No se pudo cargar el listado.');
    toast.error(err.message);
  }
}

async function search(id) {
  const { data } = await request(API + '/' + encodeURIComponent(id));
  state.searchId = id;
  renderRows([data]);
  $('search-banner').hidden = false;
  $('pager').hidden = true;
}

const refresh = () => (state.searchId ? search(state.searchId).catch(loadList) : loadList());

// --- Toolbar ---
$('search').addEventListener('submit', async e => {
  e.preventDefault();
  const input = $('search-id'), id = input.value.trim();
  if (!id) return input.focus();
  try { await search(id); } catch (err) { toast.error(err.message); }
});

$('clear-search').addEventListener('click', () => { $('search-id').value = ''; loadList(); });

$('filter').addEventListener('change', e => {
  state.filter = e.target.value;
  state.page = 1;
  $('search-id').value = '';
  loadList();
});

$('prev').addEventListener('click', () => { state.page--; loadList(); });
$('next').addEventListener('click', () => { state.page++; loadList(); });

// --- Dialogs ---
function openDialog(dialog) {
  clearErrors(dialog.querySelector('form'));
  dialog.showModal();
}

for (const b of document.querySelectorAll('[data-close]')) b.addEventListener('click', () => b.closest('dialog').close());

$('open-create').addEventListener('click', () => openDialog($('create-dialog')));

$('create').addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target;
  if (!validateForm(f)) return;
  await busy(f, async () => {
    try {
      const r = await request(API, {
        method: 'POST', body: JSON.stringify({
          identificacion: f.identificacion.value, nombres: f.nombres.value, apellidos: f.apellidos.value,
          edad: Number(f.edad.value), genero: f.genero.value
        })
      });
      f.reset();
      $('create-dialog').close();
      toast.success(r.message + ' ' + fullName(r.data));
      loadList();
    } catch (err) { toast.error(err.message); }
  });
});

function openAge(person) {
  state.current = person;
  $('age-person').textContent = fullName(person) + ' · ' + person.identificacion;
  $('age').edad.value = person.edad;
  openDialog($('age-dialog'));
}

$('age').addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target;
  if (!validateForm(f)) return;
  await busy(f, async () => {
    try {
      const r = await request(API + '/' + encodeURIComponent(state.current.identificacion) + '/edad',
        { method: 'PATCH', body: JSON.stringify({ edad: Number(f.edad.value) }) });
      $('age-dialog').close();
      toast.success(r.message);
      refresh();
    } catch (err) { toast.error(err.message); }
  });
});

function openDelete(person) {
  state.current = person;
  $('delete-text').textContent = '¿Eliminar a ' + fullName(person) + ' (' + person.identificacion + ')? Esta acción no se puede deshacer.';
  openDialog($('delete-dialog'));
}

$('delete').addEventListener('submit', async e => {
  e.preventDefault();
  await busy(e.target, async () => {
    try {
      const r = await request(API + '/' + encodeURIComponent(state.current.identificacion), { method: 'DELETE' });
      $('delete-dialog').close();
      toast.success(r.message);
      $('search-id').value = '';
      loadList();
    } catch (err) { toast.error(err.message); }
  });
});

loadList();
