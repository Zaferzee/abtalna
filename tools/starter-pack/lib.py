# Helpers for authoring the starter content pack (source of Data/StarterPack/starter-content-pack.v1.json).
def P(t): return f'<p>{t}</p>'
def UL(*items): return '<ul>' + ''.join(f'<li>{i}</li>' for i in items) + '</ul>'
def OL(*items): return '<ol>' + ''.join(f'<li>{i}</li>' for i in items) + '</ol>'
def H(t): return f'<h2>{t}</h2>'
def Q(t): return f'<blockquote>{t}</blockquote>'
def IMG(name, alt): return f'<p><img src="/img/starter-pack/{name}.svg" alt="{alt}"></p>'

def lesson(objective, img, alt, sections, remember):
    html = P(f'<strong>هدف الدرس:</strong> {objective}') + IMG(img, alt)
    for head, *parts in sections:
        html += H(head) + ''.join(parts)
    html += H('تذكر') + UL(*remember)
    return html

def mcq(kind, text, options, correct, why):
    assert len(options) == 4 and 0 <= correct < 4
    return dict(kind=kind, type='single', text=text, options=options, correct=correct, explanation=why)

def tf(text, true, why):
    return dict(kind='truefalse', type='truefalse', text=text, correct=true, explanation=why)

BANNER = Q('<strong>نموذج استرشادي تجريبي — يجب استبداله أو مراجعته واعتماده من الجهة قبل الاستخدام الإنتاجي.</strong>')
