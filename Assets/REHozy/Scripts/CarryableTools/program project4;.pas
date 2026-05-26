program project4;

type
  TFunc = function(x: real): real;

var
  a2, a1, a0: real;
  funcId: integer;

function f(x: real): real;
begin
  if funcId = 1 then
    f := a2 * x * x + a1 * x + a0
  else if funcId = 2 then
    f := sin(x)
  else if funcId = 3 then
    f := exp(x)
  else
    f := sqrt(x);
end;

function Exact(a, b: real): real;
begin
  if funcId = 1 then
    Exact := a2 * (b * b * b - a * a * a) / 3
           + a1 * (b * b - a * a) / 2
           + a0 * (b - a)
  else if funcId = 2 then
    Exact := -cos(b) + cos(a)
  else if funcId = 3 then
    Exact := exp(b) - exp(a)
  else
    Exact := (2 / 3) * (b * sqrt(b) - a * sqrt(a));
end;

function Integral(g: TFunc; a, b: real; n, metod: integer): real;
var
  h, s, x: real;
  i: integer;
begin
  h := (b - a) / n;
  s := 0;

  if metod = 1 then
  begin
    for i := 0 to n - 1 do
      s := s + g(a + i * h);
    Integral := h * s;
  end
  else if metod = 2 then
  begin
    for i := 1 to n do
      s := s + g(a + i * h);
    Integral := h * s;
  end
  else if metod = 3 then
  begin
    for i := 0 to n - 1 do
      s := s + g(a + (i + 0.5) * h);
    Integral := h * s;
  end
  else if metod = 4 then
  begin
    s := g(a) + g(b);
    for i := 1 to n - 1 do
    begin
      x := a + i * h;
      s := s + 2 * g(x);
    end;
    Integral := (h / 2) * s;
  end
  else
  begin
    s := g(a) + g(b);
    for i := 1 to n - 1 do
    begin
      x := a + i * h;
      if (i mod 2) = 1 then
        s := s + 4 * g(x)
      else
        s := s + 2 * g(x);
    end;
    Integral := (h / 3) * s;
  end;
end;

procedure WriteErrors(fname, line1: string; a, b: real);
var
  outf: text;
  n: integer;
  h, Iex, e1, e2, e3, e4, e5: real;
begin
  assign(outf, fname);
  rewrite(outf);
  writeln(outf, line1);
  Iex := Exact(a, b);
  writeln(outf, 'I_exact = ', Iex:0:12);
  writeln(outf, 'h = (b-a)/n');
  writeln(outf);
  writeln(outf, 'n', #9, 'h', #9, 'E_left', #9, 'E_right', #9,
    'E_mid', #9, 'E_trap', #9, 'E_simp');
  writeln(outf, '---', #9, '---', #9, '-------', #9, '-------', #9,
    '-----', #9, '------', #9, '------');

  n := 10;
  while n <= 10000 do
  begin
    h := (b - a) / n;
    e1 := abs(Iex - Integral(@f, a, b, n, 1));
    e2 := abs(Iex - Integral(@f, a, b, n, 2));
    e3 := abs(Iex - Integral(@f, a, b, n, 3));
    e4 := abs(Iex - Integral(@f, a, b, n, 4));
    e5 := abs(Iex - Integral(@f, a, b, n, 5));
    writeln(outf, n:6, #9, h:12:6, #9, e1:12:6, #9, e2:12:6, #9,
      e3:12:6, #9, e4:12:6, #9, e5:12:6);
    n := n + 10;
  end;

  close(outf);
end;

var
  mode: integer;
  a, b: real;
  n, metod: integer;
begin
  writeln('1 - один расчёт (многочлен), 2 - таблицы погрешностей в файлы');
  write('mode = '); readln(mode);

  if mode = 2 then
  begin
    funcId := 1;
    a2 := 1; a1 := 0; a0 := 0;
    WriteErrors('errors_func1_[0,1].txt',
      'Func 1: f(x)=a2*x^2+a1*x+a0, a2=1 a1=0 a0=0, [0,1]', 0, 1);

    a2 := 1; a1 := 1; a0 := 1;
    WriteErrors('errors_func1_poly_[0,2].txt',
      'Func 1: f(x)=x^2+x+1, [0,2]', 0, 2);

    funcId := 2;
    WriteErrors('errors_func2_sin_[0,pi].txt',
      'Func 2: f(x)=sin(x), [0, pi]', 0, pi);

    funcId := 3;
    WriteErrors('errors_func3_exp_[0,1].txt',
      'Func 3: f(x)=exp(x), [0,1]', 0, 1);
    WriteErrors('errors_func3_exp_[0,2].txt',
      'Func 3: f(x)=exp(x), [0,2]', 0, 2);

    funcId := 4;
    WriteErrors('errors_sqrt_[0,1].txt',
      'Func 4: f(x)=sqrt(x), [0,1]  (особенность в x=0)', 0, 1);
    WriteErrors('errors_sqrt_[0,10].txt',
      'Func 4: f(x)=sqrt(x), [0,10]', 0, 10);
    WriteErrors('errors_sqrt_[0.1,1].txt',
      'Func 4: f(x)=sqrt(x), [0.1,1]', 0.1, 1);
    WriteErrors('errors_sqrt_[0.1,10].txt',
      'Func 4: f(x)=sqrt(x), [0.1,10]', 0.1, 10);

    writeln('Готово.');
  end
  else
  begin
    funcId := 1;
    writeln('force into application:');
    write('a2 = '); readln(a2);
    write('a1 = '); readln(a1);
    write('a0 = '); readln(a0);
    write('a  = '); readln(a);
    write('b  = '); readln(b);
    write('n  = '); readln(n);
    write('procedure (1-left, 2-right, 3-medium, 4-trapezia, 5-simpson) = ');
    readln(metod);

    writeln;
    if (metod = 5) and ((n mod 2) = 1) then
      writeln('I = Undefined (n - even number)')
    else
      writeln('Integral = ', Integral(@f, a, b, n, metod):0:10);
  end;

  writeln;
  readln;
end.
